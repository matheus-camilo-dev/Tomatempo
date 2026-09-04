using Tomatempo.Domain.Accounts;

namespace Tomatempo.Domain.Tests.Accounts;

/// <summary>Trava de 30 dias entre trocas de apelido (§11.8).</summary>
public sealed class NicknameChangePolicyTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CanChange_DevePermitir_QuandoNuncaTrocouDesdeOCadastro()
    {
        // Act
        var canChange = NicknameChangePolicy.CanChange(lastChangedAt: null, Agora);

        // Assert
        canChange.Should().BeTrue();
    }

    [Fact]
    public void CanChange_DeveRecusar_QuandoTrocouHaVinteENoveDias()
    {
        // Arrange
        var lastChangedAt = Agora.AddDays(-29);

        // Act
        var canChange = NicknameChangePolicy.CanChange(lastChangedAt, Agora);

        // Assert
        canChange.Should().BeFalse();
    }

    [Fact]
    public void CanChange_DevePermitir_QuandoCompletouExatamenteTrintaDias()
    {
        // Arrange
        var lastChangedAt = Agora.AddDays(-30);

        // Act
        var canChange = NicknameChangePolicy.CanChange(lastChangedAt, Agora);

        // Assert
        canChange.Should().BeTrue();
    }

    [Fact]
    public void NextChangeAllowedAt_DeveSerTrintaDiasDepoisDaUltimaTroca()
    {
        // Act
        var next = NicknameChangePolicy.NextChangeAllowedAt(Agora);

        // Assert
        next.Should().Be(Agora.AddDays(30));
    }
}
