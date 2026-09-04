using Tomatempo.Domain;

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
    public static DateOnly EffectiveFromForChange(DateOnly localToday) => localToday.AddDays(1);

    /// <summary>
    /// Meta vigente em um dia: a linha de maior <c>EffectiveFrom</c> que já começou.
    /// Histórico sem nenhuma linha iniciada é impossível — toda conta nasce com a meta do
    /// cadastro (§3.10) —, então não há defesa aqui: se acontecer, é para estourar alto (§6.8).
    /// </summary>
    public static int InEffectOn(IReadOnlyCollection<GoalEntry> history, DateOnly localDate) =>
        history.Where(entry => entry.EffectiveFrom <= localDate)
            .OrderBy(entry => entry.EffectiveFrom)
            .Last()
            .Pomodoros;

    /// <summary>
    /// Aplica uma edição. Uma segunda edição no mesmo dia **substitui** a linha futura
    /// em vez de empilhar outra (§2).
    /// </summary>
    public static IReadOnlyList<GoalEntry> ApplyChange(
        IReadOnlyCollection<GoalEntry> history,
        int pomodoros,
        DateOnly localToday)
    {
        var effectiveFrom = EffectiveFromForChange(localToday);

        // Descartar o que ainda não começou é o que substitui a edição anterior do mesmo dia,
        // em vez de empilhar uma segunda linha para amanhã.
        var vigentes = history
            .Where(entry => entry.EffectiveFrom < effectiveFrom)
            .OrderBy(entry => entry.EffectiveFrom)
            .ToList();

        vigentes.Add(new GoalEntry(pomodoros, effectiveFrom));
        return vigentes;
    }
}
