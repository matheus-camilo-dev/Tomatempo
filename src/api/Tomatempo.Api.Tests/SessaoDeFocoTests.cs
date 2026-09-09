using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tomatempo.Api.Services;
using Tomatempo.Api.Tests.Harness;
using Tomatempo.Domain;
using Tomatempo.Domain.Sessions;
using Tomatempo.Domain.Time;
using Tomatempo.Infrastructure;
using Tomatempo.Infrastructure.Entities;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Sessões validadas no servidor (§3.2, §3.3). Critérios da §5: "Fechar a aba no minuto 20
/// credita exatamente 20 minutos; fechar no minuto 3 credita 0", "Abrir o timer em duas abas:
/// a segunda assume, a primeira avisa e para de pontuar", "Manipular o relógio do sistema não
/// altera minuto algum" e a segunda metade do critério do onboarding, que cobra 409 nos
/// endpoints de sessão enquanto a conta não está completa.
///
/// O cliente nunca manda minuto, duração ou hora: ele abre, bate heartbeat e fecha. Todo
/// carimbo é do servidor (§7.3.3).
/// </summary>
public sealed class SessaoDeFocoTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    /// <summary>Passo entre heartbeats no teste: menor que o buraco de 90s que trunca a cadeia.</summary>
    private static readonly TimeSpan Passo = TimeSpan.FromSeconds(60);

    private static Task<HttpResponseMessage> IniciarAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/sessions/start", new { kind = "focus" });

    private async Task<Guid> AbrirAsync(HttpClient client)
    {
        var corpo = await CorpoDe(await IniciarAsync(client));
        return corpo.GetProperty("sessionId").GetGuid();
    }

    /// <summary>Avança o relógio do servidor batendo heartbeat, como um cliente vivo faria.</summary>
    private async Task BaterAteAsync(HttpClient client, Guid id, TimeSpan duracao)
    {
        for (var decorrido = TimeSpan.Zero; decorrido < duracao; decorrido += Passo)
        {
            Relogio.Advance(Passo);
            await client.PostAsync($"/api/sessions/{id}/beat", content: null);
        }
    }

    /// <summary>
    /// Preenche o dia com sessões já apuradas, escrevendo no Postgres de verdade — não é
    /// duplo, é o mesmo banco que a API usa. Serve a cenários cujo arranjo, feito pela API,
    /// esbarraria em outra regra antes de chegar na que está sendo testada.
    /// </summary>
    private async Task SemearSessoesApuradasAsync(Conta conta, int quantas)
    {
        using var escopo = Api.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TomatempoDbContext>();

        var user = await db.Users.FirstAsync(u => u.Email == conta.Email);
        var hoje = LocalDay.Of(Relogio.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone));

        for (var i = 0; i < quantas; i++)
        {
            db.FocusSessions.Add(new FocusSessionRow
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                StartedAtUtc = Relogio.GetUtcNow().AddHours(-6).AddMinutes(i * 10),
                EndedAtUtc = Relogio.GetUtcNow().AddHours(-6).AddMinutes((i * 10) + 6),
                LastHeartbeatAtUtc = Relogio.GetUtcNow().AddHours(-6).AddMinutes((i * 10) + 6),
                Status = SessionStatus.Abandoned,
                Source = SessionSource.Online,
                CountedMinutes = 6,
                RankingEligible = false,
                LocalDate = hoje,
            });
        }

        await db.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage> EntrarComGoogleAsync(HttpClient client, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/google/callback");
        request.Headers.Add(HeaderExternalIdentityReader.EmailHeader, email);
        request.Headers.Add(HeaderExternalIdentityReader.SubjectHeader, Guid.NewGuid().ToString());
        request.Headers.Add(HeaderExternalIdentityReader.EmailVerifiedHeader, "true");
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Start_DeveAbrirASessaoEDizerQuandoElaTermina()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var response = await IniciarAsync(conta.Client);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await CorpoDe(response);
        corpo.GetProperty("sessionId").GetGuid().Should().NotBeEmpty();
        corpo.GetProperty("endsAt").GetDateTimeOffset()
            .Should().Be(Relogio.GetUtcNow() + RuleConstants.FocusDuration);
        corpo.GetProperty("serverNow").GetDateTimeOffset().Should().Be(Relogio.GetUtcNow());
    }

    [Fact]
    public async Task Start_DeveResponder401_QuandoNaoHaSessaoDeUsuario()
    {
        // Act
        var response = await IniciarAsync(NovoCliente());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Start_DeveResponder409_EnquantoOOnboardingNaoTerminou()
    {
        // Arrange — conta do Google nasce sem apelido, meta e fuso, e não pontua nada até
        // completar (§3.10).
        var client = NovoCliente();
        await EntrarComGoogleAsync(client, NovoEmail());

        // Act
        var response = await IniciarAsync(client);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodigoDe(response)).Should().Be(Problems.OnboardingRequired);
    }

    [Fact]
    public async Task Start_DeveIgnorarOQueOClienteInventaNoCorpo()
    {
        // Arrange — relógio adiantado, minutos prontos, sessão datada de ontem: nada disso tem
        // campo no contrato de entrada, e o servidor carimba tudo (§5, §7.3.3).
        var conta = await CadastrarAsync();
        var mentira = new
        {
            kind = "focus",
            startedAt = Relogio.GetUtcNow().AddDays(-1),
            countedMinutes = 25,
            serverNow = Relogio.GetUtcNow().AddHours(5),
        };

        // Act
        var response = await conta.Client.PostAsJsonAsync("/api/sessions/start", mentira);

        // Assert
        var corpo = await CorpoDe(response);
        corpo.GetProperty("startedAt").GetDateTimeOffset().Should().Be(Relogio.GetUtcNow());
        corpo.GetProperty("serverNow").GetDateTimeOffset().Should().Be(Relogio.GetUtcNow());
    }

    [Fact]
    public async Task Beat_DeveDizerQuantoFalta()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var id = await AbrirAsync(conta.Client);
        Relogio.Advance(TimeSpan.FromMinutes(5));

        // Act
        var response = await conta.Client.PostAsync($"/api/sessions/{id}/beat", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await CorpoDe(response);
        corpo.GetProperty("remainingSeconds").GetInt32().Should().Be(20 * 60);
    }

    [Fact]
    public async Task Beat_DeveResponder404_NaSessaoDeOutraPessoa()
    {
        // Arrange — autorização em nível de objeto (§7.2/A01): a sessão alheia não existe
        // para quem não é dono, e a resposta não revela que ela existe.
        var dona = await CadastrarAsync();
        var intrusa = await CadastrarAsync();
        var id = await AbrirAsync(dona.Client);

        // Act
        var response = await intrusa.Client.PostAsync($"/api/sessions/{id}/beat", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Beat_DeveResponder404_QuandoOIdNemExiste()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var response = await conta.Client.PostAsync(
            $"/api/sessions/{Guid.NewGuid()}/beat", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Start_DeveEncerrarAAnterior_QuandoOutraAbaAssume()
    {
        // Arrange — sessão única (§3.3): a segunda aba assume e a primeira vira `Superseded`,
        // com o parcial dela apurado normalmente.
        var conta = await CadastrarAsync();
        var primeira = await AbrirAsync(conta.Client);
        await BaterAteAsync(conta.Client, primeira, TimeSpan.FromMinutes(10));

        // Act
        var segunda = await AbrirAsync(conta.Client);

        // Assert
        segunda.Should().NotBe(primeira);
        var beat = await conta.Client.PostAsync($"/api/sessions/{primeira}/beat", content: null);
        beat.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodigoDe(beat)).Should().Be(Problems.SessionSuperseded);
    }

    [Fact]
    public async Task Start_DeveApurarOParcialDaSessaoAssumida()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var primeira = await AbrirAsync(conta.Client);
        await BaterAteAsync(conta.Client, primeira, TimeSpan.FromMinutes(10));

        // Act
        await AbrirAsync(conta.Client);

        // Assert — os 10 minutos da primeira não somem no meio do caminho.
        var hoje = await CorpoDe(await conta.Client.GetAsync("/api/stats/today"));
        hoje.GetProperty("focusMinutes").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task Abandon_DeveCreditarVinteMinutos_QuandoFechaNoMinutoVinte()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var id = await AbrirAsync(conta.Client);
        await BaterAteAsync(conta.Client, id, TimeSpan.FromMinutes(20));

        // Act
        var response = await conta.Client.PostAsync($"/api/sessions/{id}/abandon", content: null);

        // Assert
        var corpo = await CorpoDe(response);
        corpo.GetProperty("countedMinutes").GetInt32().Should().Be(20);
        corpo.GetProperty("discarded").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Abandon_DeveCreditarZero_QuandoFechaNoMinutoTres()
    {
        // Arrange — parcial abaixo de 5 minutos é descartado inteiro (§3.2).
        var conta = await CadastrarAsync();
        var id = await AbrirAsync(conta.Client);
        await BaterAteAsync(conta.Client, id, TimeSpan.FromMinutes(3));

        // Act
        var response = await conta.Client.PostAsync($"/api/sessions/{id}/abandon", content: null);

        // Assert
        var corpo = await CorpoDe(response);
        corpo.GetProperty("countedMinutes").GetInt32().Should().Be(0);
        corpo.GetProperty("discarded").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Complete_DeveCreditarOCicloInteiroEContarParaAMeta()
    {
        // Arrange
        var conta = await CadastrarAsync(meta: 4);
        var id = await AbrirAsync(conta.Client);
        await BaterAteAsync(conta.Client, id, RuleConstants.FocusDuration);

        // Act
        var response = await conta.Client.PostAsync($"/api/sessions/{id}/complete", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await CorpoDe(response);
        corpo.GetProperty("countedMinutes").GetInt32().Should().Be(25);
        corpo.GetProperty("completedToday").GetInt32().Should().Be(1);
        corpo.GetProperty("goal").GetInt32().Should().Be(4);
        corpo.GetProperty("goalMet").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Complete_DeveVirarParcial_QuandoOCicloNaoFechou()
    {
        // Arrange — pedir conclusão aos 12 minutos não conclui nada: a apuração é do servidor,
        // e ela manda (§3.2).
        var conta = await CadastrarAsync();
        var id = await AbrirAsync(conta.Client);
        await BaterAteAsync(conta.Client, id, TimeSpan.FromMinutes(12));

        // Act
        var response = await conta.Client.PostAsync($"/api/sessions/{id}/complete", content: null);

        // Assert
        var corpo = await CorpoDe(response);
        corpo.GetProperty("countedMinutes").GetInt32().Should().Be(12);
        corpo.GetProperty("completedToday").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Complete_DeveTruncarNoUltimoHeartbeat_QuandoAAbaFechouNoMeio()
    {
        // Arrange — bateu até o minuto 10, sumiu, e só voltou para pedir conclusão aos 25.
        // Aba fechada não rende tempo fantasma (§3.2).
        var conta = await CadastrarAsync();
        var id = await AbrirAsync(conta.Client);
        await BaterAteAsync(conta.Client, id, TimeSpan.FromMinutes(10));
        Relogio.Advance(TimeSpan.FromMinutes(15));

        // Act
        var response = await conta.Client.PostAsync($"/api/sessions/{id}/complete", content: null);

        // Assert
        var corpo = await CorpoDe(response);
        corpo.GetProperty("countedMinutes").GetInt32().Should().Be(10);
        corpo.GetProperty("completedToday").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Complete_DeveResponder404_NaSessaoDeOutraPessoa()
    {
        // Arrange
        var dona = await CadastrarAsync();
        var intrusa = await CadastrarAsync();
        var id = await AbrirAsync(dona.Client);

        // Act
        var response = await intrusa.Client.PostAsync($"/api/sessions/{id}/complete", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Abandon_DeveResponder404_NaSessaoDeOutraPessoa()
    {
        // Arrange
        var dona = await CadastrarAsync();
        var intrusa = await CadastrarAsync();
        var id = await AbrirAsync(dona.Client);

        // Act
        var response = await intrusa.Client.PostAsync($"/api/sessions/{id}/abandon", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Active_DeveDevolverASessaoEmCurso()
    {
        // Arrange — é como o cliente se recupera de um recarregamento (§4).
        var conta = await CadastrarAsync();
        var id = await AbrirAsync(conta.Client);
        Relogio.Advance(TimeSpan.FromMinutes(5));

        // Act
        var response = await conta.Client.GetAsync("/api/sessions/active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await CorpoDe(response);
        corpo.GetProperty("sessionId").GetGuid().Should().Be(id);
        corpo.GetProperty("remainingSeconds").GetInt32().Should().Be(20 * 60);
    }

    [Fact]
    public async Task Active_DeveResponder204_QuandoNaoHaSessaoAberta()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var response = await conta.Client.GetAsync("/api/sessions/active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Active_NaoDeveEnxergarASessaoDeOutraPessoa()
    {
        // Arrange
        var dona = await CadastrarAsync();
        var intrusa = await CadastrarAsync();
        await AbrirAsync(dona.Client);

        // Act
        var response = await intrusa.Client.GetAsync("/api/sessions/active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Varredor_DeveEncerrarASessaoAbandonada_ApurandoOParcial()
    {
        // Arrange — bateu até o minuto 10 e a aba morreu. Passados mais de 2 minutos sem
        // batimento, o varredor encerra como `Expired` (§3.2).
        var conta = await CadastrarAsync();
        var id = await AbrirAsync(conta.Client);
        await BaterAteAsync(conta.Client, id, TimeSpan.FromMinutes(10));
        Relogio.Advance(RuleConstants.ScavengerIdleLimit + TimeSpan.FromSeconds(30));

        // Act
        using var escopo = Api.Services.CreateScope();
        await escopo.ServiceProvider.GetRequiredService<SessionScavenger>().SweepAsync(default);

        // Assert
        var beat = await conta.Client.PostAsync($"/api/sessions/{id}/beat", content: null);
        beat.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodigoDe(beat)).Should().Be(Problems.SessionExpired);

        var hoje = await CorpoDe(await conta.Client.GetAsync("/api/stats/today"));
        hoje.GetProperty("focusMinutes").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task Varredor_NaoDeveTocarEmSessaoQueAindaBate()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var id = await AbrirAsync(conta.Client);
        await BaterAteAsync(conta.Client, id, TimeSpan.FromMinutes(5));

        // Act
        using var escopo = Api.Services.CreateScope();
        await escopo.ServiceProvider.GetRequiredService<SessionScavenger>().SweepAsync(default);

        // Assert
        var beat = await conta.Client.PostAsync($"/api/sessions/{id}/beat", content: null);
        beat.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TetoDiario_DeveCreditarZero_ApartirDaDecimaSetimaSessaoDoDia()
    {
        // Arrange — teto antifraude de 16 sessões apuradas por dia local (§3.2). O excedente é
        // registrado, mas com zero minuto.
        //
        // As 16 primeiras entram direto no banco, e não pela API: dezessete sessões pela API
        // são trinta e quatro requisições fora heartbeat, e o teto de 20/min por conta (§4) —
        // que é regra, não obstáculo de teste — responderia 429 antes do cenário terminar.
        var conta = await CadastrarAsync();
        await SemearSessoesApuradasAsync(conta, RuleConstants.MaxSessionsPerLocalDay);

        // Act
        var excedente = await AbrirAsync(conta.Client);
        await BaterAteAsync(conta.Client, excedente, TimeSpan.FromMinutes(6));
        var response = await conta.Client.PostAsync($"/api/sessions/{excedente}/abandon", content: null);

        // Assert
        (await CorpoDe(response)).GetProperty("countedMinutes").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Sessao_NaoDeveValerParaORanking_QuandoOEmailNaoEstaConfirmado()
    {
        // Arrange — ranking exige e-mail confirmado (§2, `ranking_eligible`); a conta nova
        // nasce com o e-mail pendente. Meta e streak seguem valendo.
        var conta = await CadastrarAsync();
        var id = await AbrirAsync(conta.Client);
        await BaterAteAsync(conta.Client, id, RuleConstants.FocusDuration);
        await conta.Client.PostAsync($"/api/sessions/{id}/complete", content: null);

        // Act
        var hoje = await CorpoDe(await conta.Client.GetAsync("/api/stats/today"));

        // Assert
        hoje.GetProperty("focusMinutes").GetInt32().Should().Be(25);
        hoje.GetProperty("rankingMinutes").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Sessao_DeveRecusarContentTypeDeFormulario()
    {
        // Arrange — mesma porta de CSRF fechada nos outros endpoints que mudam estado (§7.2/A05).
        var conta = await CadastrarAsync();
        var comoFormulario = new StringContent(
            "", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");

        // Act
        var response = await conta.Client.PostAsync("/api/sessions/start", comoFormulario);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }
}
