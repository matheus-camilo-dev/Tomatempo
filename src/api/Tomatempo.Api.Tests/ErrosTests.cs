using System.Net;
using System.Net.Http.Json;
using System.Text;
using Tomatempo.Api.Tests.Harness;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Critério da §5: "Todo erro sai em `application/problem+json`, com `code` estável e sem
/// stack trace." O `code` é o que o front traduz — a mensagem é para humano, não para
/// máquina (§4).
/// </summary>
public sealed class ErrosTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    [Fact]
    public async Task Erro_DeveSairComoProblemJson_QuandoAEntradaEInvalida()
    {
        // Act
        var response = await NovoCliente().PostAsync("/api/auth/register", CadastroDe(NovoEmail(), "ab"));

        // Assert
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await CorpoDe(response);
        body.GetProperty("status").GetInt32().Should().Be(400);
        body.GetProperty("code").GetString().Should().Be("invalid_nickname");
    }

    [Fact]
    public async Task Erro_NaoDeveVazarDetalheInterno()
    {
        // Act
        var response = await NovoCliente().PostAsync("/api/auth/register", CadastroDe(NovoEmail(), "ab"));

        // Assert — nada de stack, exceção ou nome de tipo interno na resposta (§7.4).
        var texto = await response.Content.ReadAsStringAsync();
        texto.Should().NotContainEquivalentOf("stacktrace");
        texto.Should().NotContainEquivalentOf("exception");
        texto.Should().NotContain("Tomatempo.");
        texto.Should().NotContainEquivalentOf("npgsql");
    }

    [Fact]
    public async Task Erro_DeveSairComoProblemJson_QuandoOCorpoNaoEJsonValido()
    {
        // Arrange
        var corpoQuebrado = new StringContent("{isto não é json", Encoding.UTF8, "application/json");

        // Act
        var response = await NovoCliente().PostAsync("/api/auth/register", corpoQuebrado);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await CodigoDe(response)).Should().Be("invalid_request");
    }

    [Fact]
    public async Task Erro_DeveTrazerOsMesmosCabecalhosDeSeguranca_QueARespostaDeSucesso()
    {
        // Arrange — o tratador de exceção limpa a resposta antes de escrever, e com ela levava
        // embora os cabeçalhos de segurança (§7.2/A05).
        var corpoQuebrado = new StringContent("{isto não é json", Encoding.UTF8, "application/json");

        // Act
        var erro = await NovoCliente().PostAsync("/api/auth/register", corpoQuebrado);
        var sucesso = await NovoCliente().GetAsync("/health");

        // Assert
        foreach (var cabecalho in new[] { "X-Content-Type-Options", "X-Frame-Options", "Content-Security-Policy" })
        {
            erro.Headers.GetValues(cabecalho).Should().Equal(
                sucesso.Headers.GetValues(cabecalho),
                $"a resposta de erro precisa carregar {cabecalho} igual à de sucesso");
        }
    }

    [Fact]
    public async Task Erro_DeveResponder401ComProblemJson_QuandoFaltaSessao()
    {
        // Act
        var response = await NovoCliente().GetAsync("/api/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await CodigoDe(response)).Should().Be("unauthenticated");
    }

    [Fact]
    public async Task Erro_DeveResponder415_QuandoOTipoDeConteudoNaoESuportado()
    {
        // Arrange
        var corpo = new StringContent("email=alguem@example.com", Encoding.UTF8, "application/x-www-form-urlencoded");

        // Act
        var response = await NovoCliente().PostAsync("/api/auth/login", corpo);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Saude_DeveResponderOk_SemAutenticacao()
    {
        // Arrange — /health é requisito não funcional (§9) e não pode exigir sessão.

        // Act
        var response = await NovoCliente().GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
