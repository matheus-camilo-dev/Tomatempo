using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tomatempo.Api.Tests.Harness;
using Tomatempo.Domain;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Sincronização do que foi feito offline (§3.4). Critérios da §5: "Modo avião: 3 pomodoros
/// contam para meta/streak ao reconectar e nenhum minuto vai para o ranking" e "Reenviar o
/// mesmo lote offline não duplica nada".
///
/// Aqui o cliente manda início e duração — não tem como ser diferente, ele estava sozinho.
/// Por isso cada item passa por janela, faixa, sobreposição, teto e idempotência antes de
/// virar minuto, e nenhum deles conta para o ranking (§7.3.3).
/// </summary>
public sealed class LoteOfflineTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    private sealed record ItemOffline(string ClientSessionId, DateTimeOffset StartedAt, int DurationSeconds);

    private Task<HttpResponseMessage> EnviarAsync(HttpClient client, params ItemOffline[] itens) =>
        client.PostAsJsonAsync("/api/sessions/offline-batch", new { items = itens });

    private ItemOffline Item(
        string? id = null,
        TimeSpan? atras = null,
        int duracaoSegundos = 25 * 60) =>
        new(
            id ?? Guid.NewGuid().ToString(),
            Relogio.GetUtcNow() - (atras ?? TimeSpan.FromHours(2)),
            duracaoSegundos);

    private static IEnumerable<JsonElement> Resultados(JsonElement corpo) =>
        corpo.GetProperty("items").EnumerateArray();

    [Fact]
    public async Task Lote_DeveAceitarTresPomodorosDoModoAviao()
    {
        // Arrange — três sessões cheias, em horários que não se encostam.
        var conta = await CadastrarAsync(meta: 3);
        var itens = new[]
        {
            Item(atras: TimeSpan.FromHours(5)),
            Item(atras: TimeSpan.FromHours(4)),
            Item(atras: TimeSpan.FromHours(3)),
        };

        // Act
        var response = await EnviarAsync(conta.Client, itens);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Resultados(await CorpoDe(response)).Should().OnlyContain(i => i.GetProperty("accepted").GetBoolean());
    }

    [Fact]
    public async Task Lote_DeveContarParaAMetaEParaAStreak()
    {
        // Arrange
        var conta = await CadastrarAsync(meta: 3);

        // Act
        await EnviarAsync(
            conta.Client,
            Item(atras: TimeSpan.FromHours(5)),
            Item(atras: TimeSpan.FromHours(4)),
            Item(atras: TimeSpan.FromHours(3)));

        // Assert
        var hoje = await CorpoDe(await conta.Client.GetAsync("/api/stats/today"));
        hoje.GetProperty("completedPomodoros").GetInt32().Should().Be(3);
        hoje.GetProperty("goalMet").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Lote_NaoDeveMandarMinutoAlgumParaORanking()
    {
        // Arrange — minuto de ranking exige começo e fim validados pelo servidor (§3.4).
        var conta = await CadastrarAsync(meta: 3);

        // Act
        await EnviarAsync(conta.Client, Item(atras: TimeSpan.FromHours(5)));

        // Assert
        var hoje = await CorpoDe(await conta.Client.GetAsync("/api/stats/today"));
        hoje.GetProperty("focusMinutes").GetInt32().Should().Be(25);
        hoje.GetProperty("rankingMinutes").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Lote_NaoDeveDuplicarNada_QuandoOMesmoLoteEReenviado()
    {
        // Arrange — a rede caindo no meio do envio é o caso comum: o cliente reenvia sem saber
        // o que passou, e o reenvio precisa ser no-op (§3.4).
        var conta = await CadastrarAsync(meta: 3);
        var itens = new[] { Item(atras: TimeSpan.FromHours(5)), Item(atras: TimeSpan.FromHours(4)) };
        await EnviarAsync(conta.Client, itens);

        // Act
        var reenvio = await EnviarAsync(conta.Client, itens);

        // Assert
        Resultados(await CorpoDe(reenvio))
            .Should().OnlyContain(i => i.GetProperty("reason").GetString() == "duplicate");

        var hoje = await CorpoDe(await conta.Client.GetAsync("/api/stats/today"));
        hoje.GetProperty("completedPomodoros").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Lote_DeveRecusarSessaoMaisVelhaQueQuarentaEOitoHoras()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var response = await EnviarAsync(conta.Client, Item(atras: TimeSpan.FromHours(49)));

        // Assert
        Resultados(await CorpoDe(response)).Single()
            .GetProperty("reason").GetString().Should().Be("too_old");
    }

    [Fact]
    public async Task Lote_DeveRecusarSessaoNoFuturo()
    {
        // Arrange — relógio adiantado do cliente, além da tolerância de 2 minutos.
        var conta = await CadastrarAsync();

        // Act
        var response = await EnviarAsync(conta.Client, Item(atras: TimeSpan.FromMinutes(-30)));

        // Assert
        Resultados(await CorpoDe(response)).Single()
            .GetProperty("reason").GetString().Should().Be("in_future");
    }

    [Theory]
    [InlineData(4 * 60)]
    [InlineData(26 * 60)]
    public async Task Lote_DeveRecusarDuracaoForaDaFaixa(int duracaoSegundos)
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var response = await EnviarAsync(conta.Client, Item(duracaoSegundos: duracaoSegundos));

        // Assert
        Resultados(await CorpoDe(response)).Single()
            .GetProperty("reason").GetString().Should().Be("invalid_duration");
    }

    [Fact]
    public async Task Lote_DeveRecusarSessaoQueSobrepoeOutra()
    {
        // Arrange — dois focos ao mesmo tempo não existem; é a fraude mais barata do lote.
        var conta = await CadastrarAsync();
        var primeira = Item(atras: TimeSpan.FromHours(5));
        await EnviarAsync(conta.Client, primeira);

        var sobreposta = primeira with
        {
            ClientSessionId = Guid.NewGuid().ToString(),
            StartedAt = primeira.StartedAt.AddMinutes(10),
        };

        // Act
        var response = await EnviarAsync(conta.Client, sobreposta);

        // Assert
        Resultados(await CorpoDe(response)).Single()
            .GetProperty("reason").GetString().Should().Be("overlaps");
    }

    [Fact]
    public async Task Lote_DeveRecusarOQuePassaDoTetoDoDia()
    {
        // Arrange — teto de 16 sessões apuradas por dia local, somando online e offline (§3.2).
        var conta = await CadastrarAsync();
        var dentroDoTeto = Enumerable.Range(0, RuleConstants.MaxSessionsPerLocalDay)
            .Select(i => Item(atras: TimeSpan.FromMinutes(30 + (i * 30))))
            .ToArray();
        await EnviarAsync(conta.Client, dentroDoTeto);

        // Act
        var response = await EnviarAsync(conta.Client, Item(atras: TimeSpan.FromMinutes(20)));

        // Assert
        Resultados(await CorpoDe(response)).Single()
            .GetProperty("reason").GetString().Should().Be("daily_cap_reached");
    }

    [Fact]
    public async Task Lote_DeveJulgarItemPorItem()
    {
        // Arrange — um item ruim não derruba o lote inteiro (§3.4).
        var conta = await CadastrarAsync();

        // Act
        var response = await EnviarAsync(
            conta.Client,
            Item(atras: TimeSpan.FromHours(5)),
            Item(atras: TimeSpan.FromHours(60)));

        // Assert
        var resultados = Resultados(await CorpoDe(response)).ToList();
        resultados[0].GetProperty("accepted").GetBoolean().Should().BeTrue();
        resultados[1].GetProperty("accepted").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Lote_DeveResponder401_QuandoNaoHaSessao()
    {
        // Act
        var response = await EnviarAsync(NovoCliente(), Item());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Lote_DeveResponder409_EnquantoOOnboardingNaoTerminou()
    {
        // Arrange
        var client = NovoCliente();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/google/callback");
        request.Headers.Add(HeaderExternalIdentityReader.EmailHeader, NovoEmail());
        request.Headers.Add(HeaderExternalIdentityReader.SubjectHeader, Guid.NewGuid().ToString());
        request.Headers.Add(HeaderExternalIdentityReader.EmailVerifiedHeader, "true");
        await client.SendAsync(request);

        // Act
        var response = await EnviarAsync(client, Item());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodigoDe(response)).Should().Be(Problems.OnboardingRequired);
    }

    [Fact]
    public async Task Lote_NaoDeveEnxergarASessaoDeOutraPessoa_AoChecarSobreposicao()
    {
        // Arrange — a sobreposição é com o histórico de quem manda, e não com o de todo mundo.
        var primeira = await CadastrarAsync();
        var segunda = await CadastrarAsync();
        var item = Item(atras: TimeSpan.FromHours(5));
        await EnviarAsync(primeira.Client, item);

        // Act — mesmo horário, outra conta.
        var response = await EnviarAsync(
            segunda.Client, item with { ClientSessionId = Guid.NewGuid().ToString() });

        // Assert
        Resultados(await CorpoDe(response)).Single()
            .GetProperty("accepted").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Lote_DeveRecusarLoteGigante()
    {
        // Arrange — teto de entrada, não regra de negócio: 48h de uso cabem folgado em 100
        // itens, e sem teto um lote inventado viraria trabalho ilimitado no servidor (§7.2/A04).
        var conta = await CadastrarAsync();
        var gigante = Enumerable.Range(0, 200).Select(_ => Item()).ToArray();

        // Act
        var response = await EnviarAsync(conta.Client, gigante);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be(Problems.InvalidRequest);
    }
}
