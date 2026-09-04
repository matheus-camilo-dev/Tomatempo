using System.Net;
using System.Net.Http.Json;
using Tomatempo.Api.Tests.Harness;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Critérios da §5 cobertos aqui:
/// - "Login e recuperação respondem igual para e-mail existente e inexistente; senha errada e
///    e-mail inexistente devolvem o mesmo `code`."
/// - "Sexta tentativa de login no mesmo minuto responde 429."
/// - "O cookie de sessão sai `HttpOnly`, `Secure` e `SameSite=Lax`; o logout o invalida."
/// - "Endpoint autenticado sem cookie responde 401, e identificador de usuário vindo do cliente
///    é ignorado."
/// </summary>
public sealed class LoginESessaoTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    private static HttpContent LoginDe(string email, string senha) =>
        JsonContent.Create(new CredenciaisPayload(email, senha));

    [Fact]
    public async Task Login_DeveAutenticar_QuandoACredencialEstaCerta()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var client = NovoCliente();

        // Act
        var response = await client.PostAsync("/api/auth/login", LoginDe(conta.Email, conta.Senha));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_DeveResponderIgual_ParaSenhaErradaEParaEmailInexistente()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var senhaErrada = await NovoCliente().PostAsync("/api/auth/login", LoginDe(conta.Email, "outra-senha-9271"));
        var emailInexistente = await NovoCliente().PostAsync("/api/auth/login", LoginDe(NovoEmail(), SenhaValida));

        // Assert
        emailInexistente.StatusCode.Should().Be(senhaErrada.StatusCode);
        (await CodigoDe(emailInexistente)).Should().Be(await CodigoDe(senhaErrada));
        (await CodigoDe(senhaErrada)).Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Login_DeveResponder429_NaSextaTentativaDoMesmoMinuto()
    {
        // Arrange — e-mail sem conta: a partição do limite é o e-mail (§3.10), e assim o
        // teste não esbarra no lockout do Identity, que é outra proteção.
        var email = NovoEmail();
        var client = NovoCliente();
        var respostas = new List<HttpResponseMessage>();

        // Act
        for (var tentativa = 1; tentativa <= 6; tentativa++)
        {
            respostas.Add(await client.PostAsync("/api/auth/login", LoginDe(email, SenhaValida)));
        }

        // Assert
        respostas.Take(5).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Unauthorized);
        respostas[5].StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Login_DeveTrocarOCookieDeSessao_QuandoAutenticaDeNovo()
    {
        // Arrange
        var email = NovoEmail();
        var client = NovoCliente();
        var cadastro = await client.PostAsync("/api/auth/register", CadastroDe(email, NovoApelido()));
        var cookieDoCadastro = SessaoDe(cadastro);
        await client.PostAsync("/api/auth/logout", content: null);

        // Act
        var login = await client.PostAsync("/api/auth/login", LoginDe(email, SenhaValida));

        // Assert — sessão nova a cada login, nunca a reutilização da anterior (§7.2/A07).
        SessaoDe(login).Should().NotBeNullOrEmpty().And.NotBe(cookieDoCadastro);
    }

    [Fact]
    public async Task Login_DeveMandarOCookieHttpOnlySecureESameSiteLax()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var client = NovoCliente();

        // Act
        var response = await client.PostAsync("/api/auth/login", LoginDe(conta.Email, conta.Senha));

        // Assert
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith(AuthSchemes.SessionCookieName, StringComparison.Ordinal));
        setCookie.Should().ContainEquivalentOf("httponly");
        setCookie.Should().ContainEquivalentOf("secure");
        setCookie.Should().ContainEquivalentOf("samesite=lax");
    }

    [Fact]
    public async Task Logout_DeveInvalidarASessao()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var logout = await conta.Client.PostAsync("/api/auth/logout", content: null);

        // Assert
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await conta.Client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_DeveResponder401_QuandoNaoHaCookie()
    {
        // Act
        var response = await NovoCliente().GetAsync("/api/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Preferencias_DeveIgnorarIdentificadorDeUsuarioVindoDoCliente()
    {
        // Arrange — B manda um dono no payload e na query (§7.2/A01). O id de usuário nunca
        // sai do servidor (§7.7), então um GUID qualquer é o melhor que um atacante teria.
        var alvo = await CadastrarAsync(idioma: "pt-BR");
        var atacante = await CadastrarAsync(idioma: "pt-BR");
        var payload = JsonContent.Create(new { locale = "en", userId = Guid.NewGuid() });

        // Act
        var response = await atacante.Client.PatchAsync($"/api/me?userId={Guid.NewGuid()}", payload);

        // Assert — muda só a conta da sessão; a de A fica intacta.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var atacanteDepois = await CorpoDe(await atacante.Client.GetAsync("/api/me"));
        atacanteDepois.GetProperty("locale").GetString().Should().Be("en");
        var alvoDepois = await CorpoDe(await alvo.Client.GetAsync("/api/me"));
        alvoDepois.GetProperty("locale").GetString().Should().Be("pt-BR");
    }

    [Fact]
    public async Task Preferencias_NaoDeveAceitarValorQueDecideRegra_VindoDoCliente()
    {
        // Arrange — campos de resultado não existem no DTO de entrada (§7.3.3).
        var conta = await CadastrarAsync();
        var payload = JsonContent.Create(new
        {
            locale = "en",
            emailConfirmed = true,
            streak = new { current = 99 },
            freezeBalance = 5,
        });

        // Act
        var response = await conta.Client.PatchAsync("/api/me", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("locale").GetString().Should().Be("en");
        me.GetProperty("emailConfirmed").GetBoolean().Should().BeFalse();
        me.GetProperty("streak").GetProperty("current").GetInt32().Should().Be(0);
        me.GetProperty("freezes").GetProperty("balance").GetInt32().Should().Be(2);
    }

    private static string? SessaoDe(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(c => c.StartsWith(AuthSchemes.SessionCookieName, StringComparison.Ordinal))
            : null;
}
