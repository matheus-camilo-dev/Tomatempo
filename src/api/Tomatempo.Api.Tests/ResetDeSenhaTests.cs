using System.Net;
using System.Net.Http.Json;
using Tomatempo.Api.Tests.Harness;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Critérios da §5: "Token de reset serve uma vez, expira em 1h, e trocar a senha derruba as
/// sessões anteriores" e "Login e recuperação respondem igual para e-mail existente e inexistente".
/// </summary>
public sealed class ResetDeSenhaTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    private const string SenhaNova = "outro-tomate-8461";

    private static HttpContent EsquecerDe(string email) => JsonContent.Create(new { email });

    private static HttpContent ResetDe(string token, string senha) =>
        JsonContent.Create(new ResetPayload(token, senha));

    [Fact]
    public async Task Esquecer_DeveResponderIgual_ComOuSemContaNoEmail()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var comConta = await NovoCliente().PostAsync("/api/auth/forgot-password", EsquecerDe(conta.Email));
        var semConta = await NovoCliente().PostAsync("/api/auth/forgot-password", EsquecerDe(NovoEmail()));

        // Assert
        semConta.StatusCode.Should().Be(comConta.StatusCode);
        (await semConta.Content.ReadAsStringAsync()).Should().Be(await comConta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Esquecer_NaoDeveMandarEmail_QuandoOEnderecoNaoTemConta()
    {
        // Arrange
        var email = NovoEmail();

        // Act
        await NovoCliente().PostAsync("/api/auth/forgot-password", EsquecerDe(email));

        // Assert
        Emails.For(email).Should().BeEmpty();
    }

    [Fact]
    public async Task Resetar_DeveTrocarASenha_QuandoOTokenEValido()
    {
        // Arrange
        var conta = await CadastrarAsync();
        await conta.Client.PostAsync("/api/auth/forgot-password", EsquecerDe(conta.Email));
        var token = Emails.TokenFor(conta.Email, SentEmailKind.PasswordReset);

        // Act
        var reset = await NovoCliente().PostAsync("/api/auth/reset-password", ResetDe(token, SenhaNova));

        // Assert
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var comSenhaNova = await NovoCliente().PostAsync(
            "/api/auth/login", JsonContent.Create(new CredenciaisPayload(conta.Email, SenhaNova)));
        comSenhaNova.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Resetar_DeveInvalidarASenhaAntiga()
    {
        // Arrange
        var conta = await CadastrarAsync();
        await conta.Client.PostAsync("/api/auth/forgot-password", EsquecerDe(conta.Email));
        var token = Emails.TokenFor(conta.Email, SentEmailKind.PasswordReset);
        await NovoCliente().PostAsync("/api/auth/reset-password", ResetDe(token, SenhaNova));

        // Act
        var comSenhaAntiga = await NovoCliente().PostAsync(
            "/api/auth/login", JsonContent.Create(new CredenciaisPayload(conta.Email, conta.Senha)));

        // Assert
        comSenhaAntiga.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Resetar_DeveDerrubarAsSessoesAnteriores()
    {
        // Arrange — a sessão aberta antes do reset é justamente a de quem talvez tenha roubado
        // a senha; deixá-la viva anularia a troca (§3.10).
        var conta = await CadastrarAsync();
        await conta.Client.PostAsync("/api/auth/forgot-password", EsquecerDe(conta.Email));
        var token = Emails.TokenFor(conta.Email, SentEmailKind.PasswordReset);

        // Act
        await NovoCliente().PostAsync("/api/auth/reset-password", ResetDe(token, SenhaNova));

        // Assert
        (await conta.Client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Resetar_DeveRecusar_QuandoOTokenJaFoiUsado()
    {
        // Arrange
        var conta = await CadastrarAsync();
        await conta.Client.PostAsync("/api/auth/forgot-password", EsquecerDe(conta.Email));
        var token = Emails.TokenFor(conta.Email, SentEmailKind.PasswordReset);
        await NovoCliente().PostAsync("/api/auth/reset-password", ResetDe(token, SenhaNova));

        // Act
        var segunda = await NovoCliente().PostAsync("/api/auth/reset-password", ResetDe(token, "mais-um-tomate-7712"));

        // Assert
        segunda.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(segunda)).Should().Be("invalid_token");
    }

    [Fact]
    public async Task Resetar_DeveRecusar_QuandoPassouDeUmaHora()
    {
        // Arrange
        var conta = await CadastrarAsync();
        await conta.Client.PostAsync("/api/auth/forgot-password", EsquecerDe(conta.Email));
        var token = Emails.TokenFor(conta.Email, SentEmailKind.PasswordReset);
        Relogio.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));

        // Act
        var response = await NovoCliente().PostAsync("/api/auth/reset-password", ResetDe(token, SenhaNova));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("invalid_token");
    }

    [Fact]
    public async Task Resetar_DeveRecusarSenhaFraca_ComOMesmoCriterioDoCadastro()
    {
        // Arrange
        var conta = await CadastrarAsync();
        await conta.Client.PostAsync("/api/auth/forgot-password", EsquecerDe(conta.Email));
        var token = Emails.TokenFor(conta.Email, SentEmailKind.PasswordReset);

        // Act
        var response = await NovoCliente().PostAsync("/api/auth/reset-password", ResetDe(token, "password"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("weak_password");
    }
}
