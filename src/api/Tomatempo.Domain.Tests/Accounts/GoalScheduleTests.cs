using Tomatempo.Domain.Accounts;

namespace Tomatempo.Domain.Tests.Accounts;

/// <summary>
/// Critério da §5: "Baixar a meta às 23h não salva a streak do dia; a nova meta aparece só
/// amanhã" — a parte de agenda da meta. O fechamento do dia que consome essa meta é da
/// fatia de streak.
/// </summary>
public sealed class GoalScheduleTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 3);
    private static readonly DateOnly Amanha = new(2026, 9, 4);

    [Fact]
    public void EffectiveFromForChange_DeveSerAmanha_QuandoUsuarioEditaHoje()
    {
        // Act
        var effectiveFrom = GoalSchedule.EffectiveFromForChange(Hoje);

        // Assert
        effectiveFrom.Should().Be(Amanha);
    }

    [Fact]
    public void InEffectOn_DeveUsarAMetaDoCadastro_QuandoNuncaHouveEdicao()
    {
        // Arrange
        var history = new[] { new GoalEntry(4, Hoje) };

        // Act
        var goal = GoalSchedule.InEffectOn(history, Hoje);

        // Assert
        goal.Should().Be(4);
    }

    [Fact]
    public void InEffectOn_DeveIgnorarLinhaFutura_QuandoOUsuarioBaixaAMetaNoFimDoDia()
    {
        // Arrange — às 23h o usuário baixou de 8 para 2; a linha nova vale só amanhã.
        var history = new[] { new GoalEntry(8, Hoje), new GoalEntry(2, Amanha) };

        // Act
        var goal = GoalSchedule.InEffectOn(history, Hoje);

        // Assert
        goal.Should().Be(8);
    }

    [Fact]
    public void InEffectOn_DeveUsarALinhaNova_APartirDoDiaEmQueElaComeca()
    {
        // Arrange
        var history = new[] { new GoalEntry(8, Hoje), new GoalEntry(2, Amanha) };

        // Act
        var goal = GoalSchedule.InEffectOn(history, Amanha);

        // Assert
        goal.Should().Be(2);
    }

    [Fact]
    public void InEffectOn_DeveUsarALinhaMaisRecenteJaIniciada_QuandoHaVariasNoPassado()
    {
        // Arrange
        var history = new[]
        {
            new GoalEntry(4, new DateOnly(2026, 8, 1)),
            new GoalEntry(6, new DateOnly(2026, 8, 20)),
            new GoalEntry(8, new DateOnly(2026, 9, 1)),
        };

        // Act
        var goal = GoalSchedule.InEffectOn(history, Hoje);

        // Assert
        goal.Should().Be(8);
    }

    [Fact]
    public void ApplyChange_DeveAcrescentarLinhaParaAmanha_QuandoEPrimeiraEdicaoDoDia()
    {
        // Arrange
        var history = new[] { new GoalEntry(4, new DateOnly(2026, 8, 1)) };

        // Act
        var updated = GoalSchedule.ApplyChange(history, 8, Hoje);

        // Assert
        updated.Should().Equal(
            new GoalEntry(4, new DateOnly(2026, 8, 1)),
            new GoalEntry(8, Amanha));
    }

    [Fact]
    public void ApplyChange_DeveSubstituirALinhaFutura_QuandoHouveOutraEdicaoNoMesmoDia()
    {
        // Arrange — já editou hoje para 8; agora edita de novo para 2.
        var history = new[] { new GoalEntry(4, new DateOnly(2026, 8, 1)), new GoalEntry(8, Amanha) };

        // Act
        var updated = GoalSchedule.ApplyChange(history, 2, Hoje);

        // Assert
        updated.Should().Equal(
            new GoalEntry(4, new DateOnly(2026, 8, 1)),
            new GoalEntry(2, Amanha));
    }

    [Fact]
    public void ApplyChange_DeveManterAMetaDeHoje_QuandoOUsuarioEditaAs23h()
    {
        // Arrange
        var history = new[] { new GoalEntry(8, Hoje) };

        // Act
        var updated = GoalSchedule.ApplyChange(history, 2, Hoje);

        // Assert
        GoalSchedule.InEffectOn(updated, Hoje).Should().Be(8);
        GoalSchedule.InEffectOn(updated, Amanha).Should().Be(2);
    }
}
