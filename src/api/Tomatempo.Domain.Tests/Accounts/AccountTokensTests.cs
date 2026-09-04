using Tomatempo.Domain.Accounts;

namespace Tomatempo.Domain.Tests.Accounts;

/// <summary>
/// Critérios da §5: "Token de confirmação serve uma vez; reusado ou passadas 24h, responde
/// erro estável" e "Token de reset serve uma vez, expira em 1h". O uso único é estado e vive
/// no teste de contrato; o prazo é regra pura e vive aqui (§3.10).
/// </summary>
public sealed class AccountTokensTests
{
    private static readonly DateTimeOffset Emissao = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LifetimeOf_DeveSerVinteEQuatroHoras_ParaConfirmacaoDeEmail()
    {
        // Act
        var lifetime = AccountTokens.LifetimeOf(AccountTokenKind.EmailConfirmation);

        // Assert
        lifetime.Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public void LifetimeOf_DeveSerUmaHora_ParaResetDeSenha()
    {
        // Act
        var lifetime = AccountTokens.LifetimeOf(AccountTokenKind.PasswordReset);

        // Assert
        lifetime.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void IsValid_DeveAceitarConfirmacao_QuandoFaltaUmMinutoParaVinteEQuatroHoras()
    {
        // Act
        var valid = AccountTokens.IsValid(
            AccountTokenKind.EmailConfirmation, Emissao, Emissao.AddHours(24).AddMinutes(-1));

        // Assert
        valid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_DeveRecusarConfirmacao_QuandoPassouDeVinteEQuatroHoras()
    {
        // Act
        var valid = AccountTokens.IsValid(
            AccountTokenKind.EmailConfirmation, Emissao, Emissao.AddHours(24).AddSeconds(1));

        // Assert
        valid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_DeveAceitarReset_QuandoFaltaUmMinutoParaUmaHora()
    {
        // Act
        var valid = AccountTokens.IsValid(
            AccountTokenKind.PasswordReset, Emissao, Emissao.AddMinutes(59));

        // Assert
        valid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_DeveRecusarReset_QuandoPassouDeUmaHora()
    {
        // Act
        var valid = AccountTokens.IsValid(
            AccountTokenKind.PasswordReset, Emissao, Emissao.AddHours(1).AddSeconds(1));

        // Assert
        valid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_DeveRecusar_QuandoOTokenVeioDeUmFuturoImpossivel()
    {
        // Arrange — relógio do cliente adiantado não cria token com validade estendida:
        // a emissão é sempre do servidor, e um "issuedAt" no futuro é dado corrompido.
        var futuro = Emissao.AddMinutes(1);

        // Act
        var valid = AccountTokens.IsValid(AccountTokenKind.PasswordReset, futuro, Emissao);

        // Assert
        valid.Should().BeFalse();
    }
}
