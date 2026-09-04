using System.Net;
using System.Net.Http.Json;
using Tomatempo.Api.Tests.Harness;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Critério da §5: "`PUT /me/goal` responde `effectiveFrom` = amanhã local; segunda edição no
/// mesmo dia substitui a linha futura em vez de empilhar."
/// O relógio da suíte está em 03/09/2026 12:00 UTC (§ <see cref="TomatempoApiFactory"/>).
/// </summary>
public sealed class MetaDiariaTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    private static HttpContent MetaDe(int pomodoros) => JsonContent.Create(new { pomodoros });

    [Fact]
    public async Task AlterarMeta_DeveValerSoAPartirDeAmanha()
    {
        // Arrange
        var conta = await CadastrarAsync(meta: 8);

        // Act
        var response = await conta.Client.PutAsync("/api/me/goal", MetaDe(2));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await CorpoDe(response);
        body.GetProperty("pomodoros").GetInt32().Should().Be(2);
        body.GetProperty("effectiveFrom").GetString().Should().Be("2026-09-04");
    }

    [Fact]
    public async Task AlterarMeta_NaoDeveMudarAMetaDeHoje()
    {
        // Arrange — é a proteção contra baixar a meta às 23h para salvar a streak.
        var conta = await CadastrarAsync(meta: 8);

        // Act
        await conta.Client.PutAsync("/api/me/goal", MetaDe(2));

        // Assert
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("goal").GetProperty("current").GetInt32().Should().Be(8);
        me.GetProperty("goal").GetProperty("upcoming").GetInt32().Should().Be(2);
        me.GetProperty("goal").GetProperty("upcomingEffectiveFrom").GetString().Should().Be("2026-09-04");
    }

    [Fact]
    public async Task AlterarMeta_DeveSubstituirALinhaFutura_QuandoEditaDuasVezesNoMesmoDia()
    {
        // Arrange
        var conta = await CadastrarAsync(meta: 8);
        await conta.Client.PutAsync("/api/me/goal", MetaDe(2));

        // Act
        var response = await conta.Client.PutAsync("/api/me/goal", MetaDe(6));

        // Assert
        (await CorpoDe(response)).GetProperty("effectiveFrom").GetString().Should().Be("2026-09-04");
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("goal").GetProperty("current").GetInt32().Should().Be(8);
        me.GetProperty("goal").GetProperty("upcoming").GetInt32().Should().Be(6);
    }

    [Theory]
    // 12:00 UTC do dia 3 é o dia 4 em Kiritimati (UTC+14) e ainda o dia 3 em Midway (UTC-11).
    [InlineData("Pacific/Kiritimati", "2026-09-05")]
    [InlineData("America/Sao_Paulo", "2026-09-04")]
    [InlineData("Pacific/Midway", "2026-09-04")]
    public async Task AlterarMeta_DeveUsarODiaLocalDoUsuario(string fuso, string esperado)
    {
        // Arrange
        var conta = await CadastrarAsync(fuso: fuso);

        // Act
        var response = await conta.Client.PutAsync("/api/me/goal", MetaDe(6));

        // Assert
        (await CorpoDe(response)).GetProperty("effectiveFrom").GetString().Should().Be(esperado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(-2)]
    public async Task AlterarMeta_DeveRecusar_QuandoEstaForaDaFaixa(int pomodoros)
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var response = await conta.Client.PutAsync("/api/me/goal", MetaDe(pomodoros));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("invalid_goal");
    }

    [Fact]
    public async Task AlterarMeta_DeveAceitarValorForaDoCardapioDoCadastro()
    {
        // Arrange — o cardápio de 2/4/6/8 é do cadastro; a edição usa a faixa do banco (§2).
        var conta = await CadastrarAsync();

        // Act
        var response = await conta.Client.PutAsync("/api/me/goal", MetaDe(5));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CorpoDe(response)).GetProperty("pomodoros").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task AlterarMeta_DeveResponder401_QuandoNaoHaSessao()
    {
        // Act
        var response = await NovoCliente().PutAsync("/api/me/goal", MetaDe(4));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
