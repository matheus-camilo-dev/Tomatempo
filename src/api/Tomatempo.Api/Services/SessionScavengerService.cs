using Microsoft.Extensions.DependencyInjection;

namespace Tomatempo.Api.Services;

/// <summary>
/// Acorda o varredor a cada minuto (§3.2). A regra inteira mora em
/// <see cref="SessionScavenger"/>; aqui só existe o relógio.
///
/// Não sobe no ambiente de teste: uma varredura acordando sozinha no meio de um teste de
/// contrato encerraria a sessão que o cenário acabou de abrir, e a suíte deixaria de ser
/// determinística. Os testes chamam <c>SweepAsync</c> no instante que escolhem.
/// </summary>
public sealed class SessionScavengerService(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<SessionScavengerService> log) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo, clock);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var escopo = scopes.CreateScope();
                var varredor = escopo.ServiceProvider.GetRequiredService<SessionScavenger>();

                var encerradas = await varredor.SweepAsync(stoppingToken);
                if (encerradas > 0) log.LogInformation("Varredor encerrou {Encerradas} sessões", encerradas);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception erro)
            {
                // Uma varredura que falha não pode derrubar o serviço: a próxima pega as
                // mesmas sessões órfãs um minuto depois.
                log.LogError(erro, "Falha na varredura de sessões");
            }
        }
    }
}
