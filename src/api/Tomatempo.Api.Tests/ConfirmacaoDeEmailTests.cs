using System.Net;
using System.Net.Http.Json;
using Tomatempo.Api.Tests.Harness;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Critério da §5: "Token de confirmação serve uma vez; reusado ou passadas 24h, responde erro
/// estável sem confirmar nada."
/// </summary>
public sealed class ConfirmacaoDeEmailTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    private static HttpContent TokenDe(string token) => JsonContent.Create(new { token });

    [Fact]
    public async Task Confirmar_DeveConfirmarOEmail_QuandoOTokenEValido()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var token = Emails.TokenFor(conta.Email, SentEmailKind.EmailConfirmation);

        // Act
        var response = await NovoCliente().PostAsync("/api/auth/confirm-email", TokenDe(token));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("emailConfirmed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Confirmar_DeveRecusar_QuandoOTokenJaFoiUsado()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var token = Emails.TokenFor(conta.Email, SentEmailKind.EmailConfirmation);
        await NovoCliente().PostAsync("/api/auth/confirm-email", TokenDe(token));

        // Act
        var response = await NovoCliente().PostAsync("/api/auth/confirm-email", TokenDe(token));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("invalid_token");
    }

    [Fact]
    public async Task Confirmar_DeveRecusar_QuandoPassaramVinteEQuatroHoras()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var token = Emails.TokenFor(conta.Email, SentEmailKind.EmailConfirmation);
        Relogio.Advance(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1));

        // Act
        var response = await NovoCliente().PostAsync("/api/auth/confirm-email", TokenDe(token));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("invalid_token");
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("emailConfirmed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Confirmar_DeveRecusar_QuandoOTokenEInventado()
    {
        // Act
        var response = await NovoCliente().PostAsync("/api/auth/confirm-email", TokenDe("token-que-nunca-existiu"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("invalid_token");
    }

    [Fact]
    public async Task Reenviar_DeveMandarOutroToken_QuandoOEmailTemConta()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var primeiro = Emails.TokenFor(conta.Email, SentEmailKind.EmailConfirmation);

        // Act
        var response = await conta.Client.PostAsync(
            "/api/auth/resend-confirmation", JsonContent.Create(new { email = conta.Email }));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Emails.TokenFor(conta.Email, SentEmailKind.EmailConfirmation).Should().NotBe(primeiro);
    }

    [Fact]
    public async Task Reenviar_DeveResponder202SemMandarNada_QuandoOEmailNaoTemConta()
    {
        // Arrange
        var email = NovoEmail();

        // Act
        var response = await NovoCliente()
            .PostAsync("/api/auth/resend-confirmation", JsonContent.Create(new { email }));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Emails.For(email).Should().BeEmpty();
    }
}
