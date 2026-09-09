using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tomatempo.Infrastructure;

namespace Tomatempo.Api.Services;

/// <summary>
/// Acorda o fechamento de hora em hora (§3.6). A regra inteira mora em
/// <see cref="DailyCloser"/>; aqui só existe o relógio e a trava.
///
/// A trava é um advisory lock do Postgres (§1.2): com mais de uma instância no ar, duas
/// tentariam fechar o mesmo dia ao mesmo tempo e a streak somaria duas vezes. Quem não pega a
/// trava simplesmente não faz nada nesta rodada — a próxima é daqui a uma hora.
///
/// Não sobe no ambiente de teste: um fechamento acordando sozinho selaria o dia que o cenário
/// está montando.
/// </summary>
public sealed class DailyCloserService(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<DailyCloserService> log) : BackgroundService
{
    /// <summary>Chave da trava; qualquer número serve, desde que seja só deste job.</summary>
    private const long AdvisoryLockKey = 8_270_101;

    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo, clock);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RodarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception erro)
            {
                // Uma rodada que falha não derruba o serviço: os dias continuam pendentes e a
                // próxima rodada pega todos eles (§3.6).
                log.LogError(erro, "Falha no fechamento diário");
            }
        }
    }

    private async Task RodarAsync(CancellationToken cancellationToken)
    {
        using var escopo = scopes.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TomatempoDbContext>();

        // Conexão própria: a trava vive enquanto a conexão vive, e a do DbContext vai e volta
        // do pool entre as consultas do fechamento.
        await using var conexao = new NpgsqlConnection(db.Database.GetDbConnection().ConnectionString);
        await conexao.OpenAsync(cancellationToken);

        await using var trava = new NpgsqlCommand("SELECT pg_try_advisory_lock(@chave)", conexao);
        trava.Parameters.AddWithValue("chave", AdvisoryLockKey);

        var pegou = (bool)(await trava.ExecuteScalarAsync(cancellationToken))!;
        if (!pegou)
        {
            log.LogDebug("Outra instância está fechando os dias; esta rodada não faz nada");
            return;
        }

        try
        {
            var fechados = await escopo.ServiceProvider
                .GetRequiredService<DailyCloser>().CloseDueAsync(cancellationToken);

            if (fechados > 0) log.LogInformation("Fechados {Fechados} dias locais", fechados);
        }
        finally
        {
            await using var solta = new NpgsqlCommand("SELECT pg_advisory_unlock(@chave)", conexao);
            solta.Parameters.AddWithValue("chave", AdvisoryLockKey);
            await solta.ExecuteScalarAsync(cancellationToken);
        }
    }
}
