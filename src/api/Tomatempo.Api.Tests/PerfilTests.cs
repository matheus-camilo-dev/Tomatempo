using Tomatempo.Api.Tests.Harness;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Critério da §5: "`GET /me` diz se a conta tem senha, sem devolver hash, dica ou qualquer
/// parte dela".
///
/// É o que permite à tela de exclusão saber se pede a senha ou não — sem isso, o front só
/// descobriria tentando excluir e apanhando 401 (§3.10).
/// </summary>
public sealed class PerfilTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    private static Task<HttpResponseMessage> EntrarComGoogleAsync(HttpClient client, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/google/callback");
        request.Headers.Add(HeaderExternalIdentityReader.EmailHeader, email);
        request.Headers.Add(HeaderExternalIdentityReader.SubjectHeader, Guid.NewGuid().ToString());
        request.Headers.Add(HeaderExternalIdentityReader.EmailVerifiedHeader, "true");
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Perfil_DeveDizerQueTemSenha_QuandoAContaNasceuComUma()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));

        // Assert
        me.GetProperty("hasPassword").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Perfil_DeveDizerQueNaoTemSenha_QuandoAContaVeioDoGoogle()
    {
        // Arrange
        var client = NovoCliente();
        await EntrarComGoogleAsync(client, NovoEmail());

        // Act
        var me = await CorpoDe(await client.GetAsync("/api/me"));

        // Assert
        me.GetProperty("hasPassword").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Perfil_NaoDeveDevolverASenhaNemPedacoDela()
    {
        // Arrange — o booleano é derivado; o hash e a senha continuam sem sair do servidor.
        var conta = await CadastrarAsync();

        // Act
        var corpo = await (await conta.Client.GetAsync("/api/me")).Content.ReadAsStringAsync();

        // Assert
        corpo.Should().NotContain(conta.Senha);
        corpo.Should().NotContainAny("passwordHash", "PasswordHash", "$argon2", "AQAAAA");
    }
}
