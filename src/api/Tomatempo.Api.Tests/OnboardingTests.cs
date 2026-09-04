using System.Net;
using System.Net.Http.Json;
using Tomatempo.Api.Tests.Harness;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Critério da §5: "Conta criada pelo Google responde `onboardingRequired` até escolher
/// apelido, meta e fuso, e nesse estado os endpoints de sessão respondem 409."
/// A parte dos endpoints de sessão fica para a fatia que os cria — eles ainda não existem.
/// </summary>
public sealed class OnboardingTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    private static HttpContent OnboardingDe(
        string apelido,
        int meta = 4,
        string fuso = "America/Sao_Paulo",
        string idioma = "pt-BR") =>
        JsonContent.Create(new { nickname = apelido, goal = meta, timeZone = fuso, locale = idioma });

    private async Task<HttpClient> ContaDoGoogleAsync()
    {
        var client = NovoCliente();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/google/callback");
        request.Headers.Add(HeaderExternalIdentityReader.EmailHeader, NovoEmail());
        request.Headers.Add(HeaderExternalIdentityReader.SubjectHeader, Guid.NewGuid().ToString());
        request.Headers.Add(HeaderExternalIdentityReader.EmailVerifiedHeader, "true");
        await client.SendAsync(request);
        return client;
    }

    [Fact]
    public async Task Onboarding_DeveCompletarAConta_QuandoOsDadosSaoValidos()
    {
        // Arrange
        var client = await ContaDoGoogleAsync();
        var apelido = NovoApelido();

        // Act
        var response = await client.PostAsync("/api/auth/onboarding", OnboardingDe(apelido, meta: 6));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var me = await CorpoDe(await client.GetAsync("/api/me"));
        me.GetProperty("onboardingRequired").GetBoolean().Should().BeFalse();
        me.GetProperty("nickname").GetString().Should().Be(apelido);
        me.GetProperty("goal").GetProperty("current").GetInt32().Should().Be(6);
    }

    [Fact]
    public async Task Onboarding_DeveRecusar_QuandoAContaJaEstaCompleta()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var response = await conta.Client.PostAsync("/api/auth/onboarding", OnboardingDe(NovoApelido()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodigoDe(response)).Should().Be("onboarding_already_completed");
    }

    [Fact]
    public async Task Onboarding_DeveRecusar_QuandoOApelidoJaEDeOutraPessoa()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var client = await ContaDoGoogleAsync();

        // Act
        var response = await client.PostAsync("/api/auth/onboarding", OnboardingDe(conta.Apelido));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodigoDe(response)).Should().Be("nickname_taken");
    }

    [Fact]
    public async Task Onboarding_DeveRecusarMeta_QuandoNaoEstaNoCardapio()
    {
        // Arrange
        var client = await ContaDoGoogleAsync();

        // Act
        var response = await client.PostAsync("/api/auth/onboarding", OnboardingDe(NovoApelido(), meta: 3));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("invalid_goal");
    }

    [Fact]
    public async Task Onboarding_DeveResponder401_QuandoNaoHaSessao()
    {
        // Act
        var response = await NovoCliente().PostAsync("/api/auth/onboarding", OnboardingDe(NovoApelido()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Onboarding_DeveDeixarAContaZerada_ComoQualquerCadastro()
    {
        // Arrange
        var client = await ContaDoGoogleAsync();

        // Act
        await client.PostAsync("/api/auth/onboarding", OnboardingDe(NovoApelido()));

        // Assert
        var me = await CorpoDe(await client.GetAsync("/api/me"));
        me.GetProperty("streak").GetProperty("current").GetInt32().Should().Be(0);
        me.GetProperty("freezes").GetProperty("balance").GetInt32().Should().Be(2);
    }
}
