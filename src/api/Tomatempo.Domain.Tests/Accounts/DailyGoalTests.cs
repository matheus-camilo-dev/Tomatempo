using Tomatempo.Domain.Accounts;

namespace Tomatempo.Domain.Tests.Accounts;

/// <summary>
/// Meta diária: cardápio fechado no cadastro (§3.5) e faixa aberta do banco na edição (§2).
/// </summary>
public sealed class DailyGoalTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void ValidateSignup_DeveAceitar_QuandoEUmaDasQuatroOpcoes(int pomodoros)
    {
        // Act
        var error = DailyGoal.ValidateSignup(pomodoros);

        // Assert
        error.Should().Be(GoalError.None);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(24)]
    [InlineData(-4)]
    public void ValidateSignup_DeveRecusar_QuandoNaoEstaNoCardapio(int pomodoros)
    {
        // Act
        var error = DailyGoal.ValidateSignup(pomodoros);

        // Assert
        error.Should().Be(GoalError.NotOfferedAtSignup);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(24)]
    public void ValidateChange_DeveAceitar_QuandoEstaDentroDaFaixa(int pomodoros)
    {
        // Act
        var error = DailyGoal.ValidateChange(pomodoros);

        // Assert
        error.Should().Be(GoalError.None);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void ValidateChange_DeveRecusar_QuandoEstaForaDaFaixa(int pomodoros)
    {
        // Act
        var error = DailyGoal.ValidateChange(pomodoros);

        // Assert
        error.Should().Be(GoalError.OutOfRange);
    }
}
