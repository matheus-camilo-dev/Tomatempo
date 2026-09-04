using System.Net;
using System.Net.Http.Json;
using Tomatempo.Api.Tests.Harness;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Critérios da §5 cobertos aqui:
/// - "O cadastro credita 2 freezes com linha `Signup` no ledger, streak 0 e nenhum minuto."
/// - "Conta nova criada após uso anônimo começa zerada."
/// - "Cadastro com e-mail já usado responde igual ao de e-mail novo e não cria a segunda conta."
/// - "Apelido fora da whitelist, curto demais, longo demais ou repetido em outra caixa é recusado."
/// - "Senha com menos de 8 caracteres ou presente na lista de senhas comuns é recusada."
/// </summary>
public sealed class CadastroTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    [Fact]
    public async Task Cadastrar_DeveComecarZerado_QuandoContaENova()
    {
        // Arrange & Act
        var conta = await CadastrarAsync(meta: 6);

        // Assert
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("streak").GetProperty("current").GetInt32().Should().Be(0);
        me.GetProperty("streak").GetProperty("longest").GetInt32().Should().Be(0);
        me.GetProperty("goal").GetProperty("current").GetInt32().Should().Be(6);
    }

    [Fact]
    public async Task Cadastrar_DeveCreditarDoisCongelamentosComLinhaSignup()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var response = await conta.Client.GetAsync("/api/me/freezes");

        // Assert
        var body = await CorpoDe(response);
        body.GetProperty("balance").GetInt32().Should().Be(2);
        var entries = body.GetProperty("entries").EnumerateArray().ToArray();
        entries.Should().ContainSingle();
        entries[0].GetProperty("delta").GetInt32().Should().Be(2);
        entries[0].GetProperty("reason").GetString().Should().Be("Signup");
    }

    [Fact]
    public async Task Cadastrar_DeveEntrarLogadoComEmailNaoConfirmado()
    {
        // Arrange & Act
        var conta = await CadastrarAsync();

        // Assert
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("emailConfirmed").GetBoolean().Should().BeFalse();
        me.GetProperty("onboardingRequired").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Cadastrar_DeveEnviarEmailDeConfirmacao()
    {
        // Arrange & Act
        var conta = await CadastrarAsync();

        // Assert
        Emails.For(conta.Email).Should().ContainSingle()
            .Which.Kind.Should().Be(SentEmailKind.EmailConfirmation);
    }

    [Fact]
    public async Task Cadastrar_DeveResponderIgual_QuandoOEmailJaTemConta()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var primeira = await NovoCliente().PostAsync("/api/auth/register", CadastroDe(NovoEmail(), NovoApelido()));

        // Act — mesmo e-mail, apelido diferente.
        var segunda = await NovoCliente().PostAsync("/api/auth/register", CadastroDe(conta.Email, NovoApelido()));

        // Assert
        segunda.StatusCode.Should().Be(primeira.StatusCode);
        (await segunda.Content.ReadAsStringAsync()).Should().Be(await primeira.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cadastrar_NaoDeveCriarSegundaConta_QuandoOEmailJaTemConta()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var apelidoDaTentativa = NovoApelido();

        // Act
        await NovoCliente().PostAsync("/api/auth/register", CadastroDe(conta.Email, apelidoDaTentativa));

        // Assert — o apelido da tentativa recusada continua livre para outra pessoa.
        var outra = await NovoCliente().PostAsync("/api/auth/register", CadastroDe(NovoEmail(), apelidoDaTentativa));
        outra.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Cadastrar_DeveAvisarODonoDoEmail_QuandoAlguemTentaUsarOEndereco()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        await NovoCliente().PostAsync("/api/auth/register", CadastroDe(conta.Email, NovoApelido()));

        // Assert
        Emails.For(conta.Email).Select(e => e.Kind).Should().Equal(
            SentEmailKind.EmailConfirmation,
            SentEmailKind.DuplicateRegistrationNotice);
    }

    [Fact]
    public async Task Cadastrar_NaoDeveDarSessao_QuandoOEmailJaTemConta()
    {
        // Arrange
        var conta = await CadastrarAsync();
        var client = NovoCliente();

        // Act
        await client.PostAsync("/api/auth/register", CadastroDe(conta.Email, NovoApelido()));

        // Assert
        var me = await client.GetAsync("/api/me");
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Cadastrar_DeveResponder429_NaSextaTentativaComOMesmoEmail()
    {
        // Arrange
        var email = NovoEmail();
        var respostas = new List<HttpResponseMessage>();

        // Act
        for (var tentativa = 1; tentativa <= 6; tentativa++)
        {
            respostas.Add(await NovoCliente().PostAsync("/api/auth/register", CadastroDe(email, NovoApelido())));
        }

        // Assert
        respostas.Take(5).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Accepted);
        respostas[5].StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("ana silva")]
    [InlineData("ana-silva")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaa")]
    public async Task Cadastrar_DeveRecusarApelido_QuandoNaoRespeitaAWhitelist(string apelido)
    {
        // Act
        var response = await NovoCliente().PostAsync("/api/auth/register", CadastroDe(NovoEmail(), apelido));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("invalid_nickname");
    }

    [Fact]
    public async Task Cadastrar_DeveRecusarApelido_QuandoJaExisteEmOutraCaixa()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var response = await NovoCliente()
            .PostAsync("/api/auth/register", CadastroDe(NovoEmail(), conta.Apelido.ToUpperInvariant()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodigoDe(response)).Should().Be("nickname_taken");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(0)]
    [InlineData(25)]
    public async Task Cadastrar_DeveRecusarMeta_QuandoNaoEstaNoCardapio(int meta)
    {
        // Act
        var response = await NovoCliente()
            .PostAsync("/api/auth/register", CadastroDe(NovoEmail(), NovoApelido(), meta: meta));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("invalid_goal");
    }

    [Fact]
    public async Task Cadastrar_DeveRecusarFuso_QuandoNaoEUmFusoIana()
    {
        // Act
        var response = await NovoCliente()
            .PostAsync("/api/auth/register", CadastroDe(NovoEmail(), NovoApelido(), fuso: "Marte/Olympus"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("invalid_time_zone");
    }

    [Fact]
    public async Task Cadastrar_DeveRecusarIdioma_QuandoNaoESuportado()
    {
        // Act
        var response = await NovoCliente()
            .PostAsync("/api/auth/register", CadastroDe(NovoEmail(), NovoApelido(), idioma: "fr-FR"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("invalid_locale");
    }

    [Theory]
    [InlineData("curta12")]
    [InlineData("1234567")]
    public async Task Cadastrar_DeveRecusarSenha_QuandoTemMenosDeOitoCaracteres(string senha)
    {
        // Act
        var response = await NovoCliente()
            .PostAsync("/api/auth/register", CadastroDe(NovoEmail(), NovoApelido(), senha: senha));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("weak_password");
    }

    [Theory]
    [InlineData("password")]
    [InlineData("12345678")]
    [InlineData("qwertyui")]
    public async Task Cadastrar_DeveRecusarSenha_QuandoEstaNaListaDeComuns(string senha)
    {
        // Act
        var response = await NovoCliente()
            .PostAsync("/api/auth/register", CadastroDe(NovoEmail(), NovoApelido(), senha: senha));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("weak_password");
    }

    [Fact]
    public async Task Cadastrar_DeveRecusarSenha_QuandoPassaDoTamanhoMaximo()
    {
        // Arrange — a senha alimenta um hash caro de propósito; sem teto, o tamanho da
        // entrada vira alavanca de consumo (§3.10).
        var senhaEnorme = new string('a', 129);

        // Act
        var response = await NovoCliente()
            .PostAsync("/api/auth/register", CadastroDe(NovoEmail(), NovoApelido(), senha: senhaEnorme));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("weak_password");
    }

    [Fact]
    public async Task Cadastrar_DeveRecusarEmail_QuandoPassaDoTamanhoMaximo()
    {
        // Arrange — 254 é o limite da RFC 5321; acima disso o endereço não cabe na coluna e
        // hoje virava erro interno em vez de recusa explícita.
        var emailEnorme = new string('a', 243) + "@example.com";

        // Act
        var response = await NovoCliente()
            .PostAsync("/api/auth/register", CadastroDe(emailEnorme, NovoApelido()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDe(response)).Should().Be("invalid_email");
    }

    [Fact]
    public async Task Cadastrar_DeveGuardarOApelidoEmMinusculas_QuandoVemComMaiusculas()
    {
        // Arrange
        var apelido = NovoApelido();
        var client = NovoCliente();

        // Act
        await client.PostAsync("/api/auth/register", CadastroDe(NovoEmail(), apelido.ToUpperInvariant()));

        // Assert
        var me = await CorpoDe(await client.GetAsync("/api/me"));
        me.GetProperty("nickname").GetString().Should().Be(apelido);
    }
}
