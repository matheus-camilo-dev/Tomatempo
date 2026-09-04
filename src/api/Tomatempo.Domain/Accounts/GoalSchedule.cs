namespace Tomatempo.Domain.Accounts;

/// <summary>Uma linha de <c>user_goals</c>: a meta e o dia local em que passa a valer (§2).</summary>
public sealed record GoalEntry(int Pomodoros, DateOnly EffectiveFrom);

/// <summary>
/// Histórico de metas — a proteção antifraude que impede baixar a meta às 23h para
/// salvar a streak do dia (§3.5, REGRAS-DE-NEGOCIO.md §3). Função pura: o dia local
/// já chega resolvido, esta camada não conhece fuso nem relógio.
/// </summary>
public static class GoalSchedule
{
    /// <summary>Uma edição feita hoje vale a partir de amanhã, nunca antes.</summary>
    public static DateOnly EffectiveFromForChange(DateOnly localToday) => throw new NotImplementedException();

    /// <summary>Meta vigente em um dia: a linha de maior <c>EffectiveFrom</c> que já começou.</summary>
    public static int InEffectOn(IReadOnlyCollection<GoalEntry> history, DateOnly localDate) =>
        throw new NotImplementedException();

    /// <summary>
    /// Aplica uma edição. Uma segunda edição no mesmo dia **substitui** a linha futura
    /// em vez de empilhar outra (§2).
    /// </summary>
    public static IReadOnlyList<GoalEntry> ApplyChange(
        IReadOnlyCollection<GoalEntry> history,
        int pomodoros,
        DateOnly localToday) => throw new NotImplementedException();
}
