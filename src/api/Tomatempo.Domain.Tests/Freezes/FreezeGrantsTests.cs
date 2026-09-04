using Tomatempo.Domain.Freezes;

namespace Tomatempo.Domain.Tests.Freezes;

/// <summary>
/// Critério da §5: "O cadastro credita 2 freezes com linha `Signup` no ledger, streak 0 e
/// nenhum minuto" — a parte do crédito (§3.10, REGRAS-DE-NEGOCIO.md §4).
/// </summary>
public sealed class FreezeGrantsTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ForSignup_DeveCreditarDoisCongelamentos()
    {
        // Act
        var entry = FreezeGrants.ForSignup(Agora);

        // Assert
        entry.Delta.Should().Be(2);
    }

    [Fact]
    public void ForSignup_DeveRegistrarOMotivoSignup()
    {
        // Act
        var entry = FreezeGrants.ForSignup(Agora);

        // Assert
        entry.Reason.Should().Be(FreezeReason.Signup);
    }

    [Fact]
    public void ForSignup_NaoDeveAmarrarOCreditoAUmDia()
    {
        // Arrange — o crédito do cadastro não pertence a nenhum dia local, ao contrário do
        // consumo por dia coberto; a data de referência fica vazia (§2).

        // Act
        var entry = FreezeGrants.ForSignup(Agora);

        // Assert
        entry.RefDate.Should().BeNull();
        entry.CreatedAt.Should().Be(Agora);
    }
}
