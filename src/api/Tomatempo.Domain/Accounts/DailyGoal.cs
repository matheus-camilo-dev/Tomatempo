using Tomatempo.Domain;

namespace Tomatempo.Domain.Accounts;

/// <summary>Motivo da recusa de uma meta diária. <see cref="None"/> significa aceita.</summary>
public enum GoalError
{
    None,
    NotOfferedAtSignup,
    OutOfRange,
}

/// <summary>
/// Meta diária em pomodoros concluídos (§3.5). O cadastro oferece um cardápio fechado
/// (2/4/6/8); a edição posterior aceita a faixa que o banco guarda (§2, `user_goals`).
/// </summary>
public static class DailyGoal
{
    /// <summary>Meta escolhida no cadastro: só os quatro valores oferecidos.</summary>
    public static GoalError ValidateSignup(int pomodoros) =>
        RuleConstants.AllowedSignupGoals.Contains(pomodoros) ? GoalError.None : GoalError.NotOfferedAtSignup;

    /// <summary>Meta trocada depois, pela tela de preferências.</summary>
    public static GoalError ValidateChange(int pomodoros) =>
        pomodoros >= RuleConstants.MinDailyGoal && pomodoros <= RuleConstants.MaxDailyGoal
            ? GoalError.None
            : GoalError.OutOfRange;
}
