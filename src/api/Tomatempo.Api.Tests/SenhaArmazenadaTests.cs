using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tomatempo.Api.Tests.Harness;
using Tomatempo.Infrastructure;
using Tomatempo.Infrastructure.Entities;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Critérios da §5: "A senha é guardada em Argon2id; conta com hash PBKDF2 antigo continua
/// entrando e sai do login já regravada em Argon2id."
///
/// O teste lê o hash direto do banco de propósito: é a única forma de afirmar o que ficou
/// gravado, e é justamente o que um vazamento de dump exporia (§7.2/A02).
/// </summary>
public sealed class SenhaArmazenadaTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    private const string PrefixoArgon2id = "$argon2id$";

    [Fact]
    public async Task Cadastrar_DeveGuardarASenhaComArgon2id()
    {
        // Arrange & Act
        var conta = await CadastrarAsync();

        // Assert
        var hash = await HashDe(conta.Email);
        hash.Should().StartWith(PrefixoArgon2id);
    }

    [Fact]
    public async Task Cadastrar_NaoDeveGuardarASenhaEmTextoAlgumRecuperavel()
    {
        // Arrange & Act
        var conta = await CadastrarAsync();

        // Assert — nem a senha, nem qualquer pedaço dela, aparecem no que foi gravado.
        var hash = await HashDe(conta.Email);
        hash.Should().NotContain(conta.Senha);
        hash.Should().NotContainEquivalentOf("tomate");
    }

    [Fact]
    public async Task Login_DeveFuncionar_QuandoAContaAindaTemHashPbkdf2()
    {
        // Arrange
        var conta = await CadastrarAsync();
        await RebaixarParaPbkdf2Async(conta.Email, conta.Senha);

        // Act
        var login = await NovoCliente().PostAsync(
            "/api/auth/login", JsonContent.Create(new CredenciaisPayload(conta.Email, conta.Senha)));

        // Assert
        login.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Login_DeveRegravarEmArgon2id_QuandoAContaAindaTemHashPbkdf2()
    {
        // Arrange — a migração acontece sozinha, sem ninguém precisar trocar de senha.
        var conta = await CadastrarAsync();
        await RebaixarParaPbkdf2Async(conta.Email, conta.Senha);

        // Act
        await NovoCliente().PostAsync(
            "/api/auth/login", JsonContent.Create(new CredenciaisPayload(conta.Email, conta.Senha)));

        // Assert
        var hash = await HashDe(conta.Email);
        hash.Should().StartWith(PrefixoArgon2id);
    }

    [Fact]
    public async Task Login_DeveRecusarSenhaErrada_QuandoAContaAindaTemHashPbkdf2()
    {
        // Arrange
        var conta = await CadastrarAsync();
        await RebaixarParaPbkdf2Async(conta.Email, conta.Senha);

        // Act
        var login = await NovoCliente().PostAsync(
            "/api/auth/login", JsonContent.Create(new CredenciaisPayload(conta.Email, "outra-senha-9271")));

        // Assert
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Resetar_DeveGuardarANovaSenhaComArgon2id()
    {
        // Arrange
        var conta = await CadastrarAsync();
        await conta.Client.PostAsync("/api/auth/forgot-password", JsonContent.Create(new { email = conta.Email }));
        var token = Emails.TokenFor(conta.Email, SentEmailKind.PasswordReset);

        // Act
        await NovoCliente().PostAsync(
            "/api/auth/reset-password", JsonContent.Create(new ResetPayload(token, "outro-tomate-8461")));

        // Assert
        var hash = await HashDe(conta.Email);
        hash.Should().StartWith(PrefixoArgon2id);
    }

    private async Task<string> HashDe(string email)
    {
        using var scope = Api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TomatempoDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.PasswordHash!).SingleAsync();
    }

    /// <summary>Reescreve o hash no formato antigo, como estão as contas criadas antes da troca.</summary>
    private async Task RebaixarParaPbkdf2Async(string email, string senha)
    {
        using var scope = Api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TomatempoDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);

        user.PasswordHash = new PasswordHasher<TomatempoUser>().HashPassword(user, senha);
        await db.SaveChangesAsync();
    }
}
