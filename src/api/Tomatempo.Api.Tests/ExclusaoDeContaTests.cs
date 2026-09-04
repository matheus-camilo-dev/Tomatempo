using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tomatempo.Api.Tests.Harness;
using Tomatempo.Infrastructure;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Critérios da §5: "Excluir a conta exige a senha atual quando existe uma; a sessão morre, e
/// e-mail e apelido voltam a ficar livres para um cadastro novo" e "Conta criada pelo Google,
/// que não tem senha, é excluída só com a sessão" (§3.10, §7.7).
/// </summary>
public sealed class ExclusaoDeContaTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    private static HttpRequestMessage ExclusaoDe(string? senha) =>
        new(HttpMethod.Delete, "/api/me") { Content = JsonContent.Create(new { password = senha }) };

    [Fact]
    public async Task Excluir_DeveApagarAConta_QuandoASenhaConfere()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var response = await conta.Client.SendAsync(ExclusaoDe(conta.Senha));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Excluir_DeveMatarASessao()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        await conta.Client.SendAsync(ExclusaoDe(conta.Senha));

        // Assert
        (await conta.Client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Excluir_DeveLiberarOEmailEOApelidoParaUmCadastroNovo()
    {
        // Arrange
        var conta = await CadastrarAsync();
        await conta.Client.SendAsync(ExclusaoDe(conta.Senha));

        // Act — mesma pessoa recomeçando do zero, ou outra pessoa aproveitando o apelido.
        var novoCadastro = await NovoCliente()
            .PostAsync("/api/auth/register", CadastroDe(conta.Email, conta.Apelido));

        // Assert
        novoCadastro.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Excluir_DeveApagarMetaLedgerEStreakDoUsuario()
    {
        // Arrange — nada de linha órfã sobrando com o dado de quem pediu exclusão (§7.7).
        var conta = await CadastrarAsync();
        var userId = await IdDe(conta.Email);

        // Act
        await conta.Client.SendAsync(ExclusaoDe(conta.Senha));

        // Assert
        using var scope = Api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TomatempoDbContext>();
        (await db.UserGoals.CountAsync(g => g.UserId == userId)).Should().Be(0);
        (await db.FreezeLedger.CountAsync(f => f.UserId == userId)).Should().Be(0);
        (await db.UserStreaks.CountAsync(s => s.UserId == userId)).Should().Be(0);
        (await db.AccountTokens.CountAsync(t => t.UserId == userId)).Should().Be(0);
    }

    [Fact]
    public async Task Excluir_DeveRecusar_QuandoASenhaEstaErrada()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var response = await conta.Client.SendAsync(ExclusaoDe("outra-senha-9271"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodigoDe(response)).Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Excluir_NaoDeveApagarNada_QuandoASenhaEstaErrada()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        await conta.Client.SendAsync(ExclusaoDe("outra-senha-9271"));

        // Assert
        (await conta.Client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Excluir_DeveRecusar_QuandoNaoVeioSenhaEAContaTemUma()
    {
        // Arrange
        var conta = await CadastrarAsync();

        // Act
        var response = await conta.Client.SendAsync(ExclusaoDe(senha: null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodigoDe(response)).Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Excluir_DeveResponder401_QuandoNaoHaSessao()
    {
        // Act
        var response = await NovoCliente().SendAsync(ExclusaoDe(SenhaValida));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Excluir_DeveApagarContaDoGoogle_SemPedirSenha()
    {
        // Arrange — conta criada pelo provedor não tem senha para digitar.
        var client = NovoCliente();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/google/callback");
        request.Headers.Add(HeaderExternalIdentityReader.EmailHeader, NovoEmail());
        request.Headers.Add(HeaderExternalIdentityReader.SubjectHeader, Guid.NewGuid().ToString());
        request.Headers.Add(HeaderExternalIdentityReader.EmailVerifiedHeader, "true");
        await client.SendAsync(request);

        // Act
        var response = await client.SendAsync(ExclusaoDe(senha: null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<Guid> IdDe(string email)
    {
        using var scope = Api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TomatempoDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
    }
}
