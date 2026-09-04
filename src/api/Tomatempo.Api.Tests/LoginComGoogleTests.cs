using System.Net;
using System.Net.Http.Json;
using Tomatempo.Api.Tests.Harness;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Critérios da §5: "Google com e-mail já cadastrado entra na conta existente; sem
/// `email_verified` do provedor, é recusado" e "Conta criada pelo Google responde
/// `onboardingRequired` até escolher apelido, meta e fuso".
/// </summary>
public sealed class LoginComGoogleTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    private static Task<HttpResponseMessage> EntrarComGoogleAsync(
        HttpClient client,
        string email,
        string subject,
        bool emailVerificado = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/google/callback");
        request.Headers.Add(HeaderExternalIdentityReader.EmailHeader, email);
        request.Headers.Add(HeaderExternalIdentityReader.SubjectHeader, subject);
        request.Headers.Add(HeaderExternalIdentityReader.EmailVerifiedHeader, emailVerificado ? "true" : "false");
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Google_DeveCriarContaIncompleta_QuandoOEmailENovo()
    {
        // Arrange
        var client = NovoCliente();

        // Act
        var response = await EntrarComGoogleAsync(client, NovoEmail(), subject: Guid.NewGuid().ToString());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWith("/onboarding");
        var me = await CorpoDe(await client.GetAsync("/api/me"));
        me.GetProperty("onboardingRequired").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Google_DeveNascerComEmailConfirmado_QuandoOProvedorGarantiuOEndereco()
    {
        // Arrange
        var client = NovoCliente();

        // Act
        await EntrarComGoogleAsync(client, NovoEmail(), subject: Guid.NewGuid().ToString());

        // Assert
        var me = await CorpoDe(await client.GetAsync("/api/me"));
        me.GetProperty("emailConfirmed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Google_DeveCreditarOsDoisCongelamentosDoCadastro()
    {
        // Arrange
        var client = NovoCliente();

        // Act
        await EntrarComGoogleAsync(client, NovoEmail(), subject: Guid.NewGuid().ToString());

        // Assert
        var freezes = await CorpoDe(await client.GetAsync("/api/me/freezes"));
        freezes.GetProperty("balance").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Google_DeveEntrarNaContaExistente_QuandoOEmailJaTemCadastroDeSenha()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var client = NovoCliente();

        // Act
        var response = await EntrarComGoogleAsync(client, conta.Email, subject: Guid.NewGuid().ToString());

        // Assert — é a mesma conta: apelido e onboarding vieram do cadastro por senha.
        response.Headers.Location!.ToString().Should().EndWith("/");
        var me = await CorpoDe(await client.GetAsync("/api/me"));
        me.GetProperty("nickname").GetString().Should().Be(conta.Apelido);
        me.GetProperty("onboardingRequired").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Google_NaoDeveCriarSegundaConta_QuandoOEmailJaTemCadastroDeSenha()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        await EntrarComGoogleAsync(NovoCliente(), conta.Email, subject: Guid.NewGuid().ToString());

        // Assert — o e-mail continua entrando com a senha original, na mesma conta.
        var client = NovoCliente();
        var login = await client.PostAsync(
            "/api/auth/login", JsonContent.Create(new CredenciaisPayload(conta.Email, conta.Senha)));
        login.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var me = await CorpoDe(await client.GetAsync("/api/me"));
        me.GetProperty("nickname").GetString().Should().Be(conta.Apelido);
    }

    [Fact]
    public async Task Google_DeveEntrarNaMesmaConta_QuandoOProvedorJaEstaVinculado()
    {
        // Arrange
        var email = NovoEmail();
        var subject = Guid.NewGuid().ToString();
        var primeiroCliente = NovoCliente();
        await EntrarComGoogleAsync(primeiroCliente, email, subject);
        var apelido = await CompletarOnboardingAsync(primeiroCliente);

        // Act
        var segundoCliente = NovoCliente();
        var response = await EntrarComGoogleAsync(segundoCliente, email, subject);

        // Assert
        response.Headers.Location!.ToString().Should().EndWith("/");
        var me = await CorpoDe(await segundoCliente.GetAsync("/api/me"));
        me.GetProperty("nickname").GetString().Should().Be(apelido);
    }

    [Fact]
    public async Task Google_DeveRecusar_QuandoOProvedorNaoConfirmaOEmail()
    {
        // Arrange
        var client = NovoCliente();

        // Act
        var response = await EntrarComGoogleAsync(
            client, NovoEmail(), subject: Guid.NewGuid().ToString(), emailVerificado: false);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWith("/entrar?erro=external_email_not_verified");
    }

    [Fact]
    public async Task Google_NaoDeveDarSessao_QuandoOProvedorNaoConfirmaOEmail()
    {
        // Arrange
        var client = NovoCliente();

        // Act
        await EntrarComGoogleAsync(client, NovoEmail(), subject: Guid.NewGuid().ToString(), emailVerificado: false);

        // Assert
        (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Google_NaoDeveTomarAContaAlheia_QuandoOEmailNaoEVerificado()
    {
        // Arrange — o vetor: alguém diz ao provedor que é dono de um e-mail já cadastrado.
        var conta = await CadastrarAsync();
        var client = NovoCliente();

        // Act
        await EntrarComGoogleAsync(client, conta.Email, subject: Guid.NewGuid().ToString(), emailVerificado: false);

        // Assert
        (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<string> CompletarOnboardingAsync(HttpClient client)
    {
        var apelido = NovoApelido();
        var response = await client.PostAsync("/api/auth/onboarding", JsonContent.Create(new
        {
            nickname = apelido,
            goal = 4,
            timeZone = "America/Sao_Paulo",
            locale = "pt-BR",
        }));
        response.IsSuccessStatusCode.Should().BeTrue("o onboarding do cenário precisa ter dado certo");
        return apelido;
    }
}
