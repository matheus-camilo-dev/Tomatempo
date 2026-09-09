using Tomatempo.Domain.Freezes;

namespace Tomatempo.Domain.Streaks;

/// <summary>
/// O que a linha de <c>user_streaks</c> guarda (§2). <c>ConsecutiveGoalDays</c> é o contador
/// do marco de 7, e não a streak: dia coberto por congelamento mantém a streak e zera o
/// marco.
/// </summary>
public sealed record StreakState(
    int CurrentStreak,
    int LongestStreak,
    int ConsecutiveGoalDays,
    int FreezeBalance);

/// <summary>Estado novo e as linhas de ledger que o fechamento gerou.</summary>
public sealed record DayClosingResult(StreakState State, IReadOnlyList<FreezeLedgerEntry> Ledger);

/// <summary>
/// Fechamento de um dia local (§3.6). Função pura: o dia já chega resolvido e a meta já
/// chega comparada — esta camada não conhece fuso, relógio nem banco.
/// </summary>
public static class DayClosing
{
    public static DayClosingResult Close(
        StreakState state,
        bool goalMet,
        DateOnly day,
        DateTimeOffset now)
    {
        var ledger = new List<FreezeLedgerEntry>();

        var (streak, consecutivos, saldo) = goalMet
            ? (state.CurrentStreak + 1, state.ConsecutiveGoalDays + 1, state.FreezeBalance)
            : Cobrir(state, day, now, ledger);

        if (goalMet && consecutivos % RuleConstants.FreezeMilestoneDays == 0)
        {
            saldo = Creditar(saldo, FreezeReason.StreakMilestone, day, now, ledger);
        }

        return new DayClosingResult(
            new StreakState(
                streak,
                Math.Max(state.LongestStreak, streak),
                consecutivos,
                saldo),
            ledger);
    }

    /// <summary>
    /// Dia sem meta batida. Um congelamento salva a streak; o marco de 7 volta a zero, senão
    /// daria para farmar congelamento com congelamento (§3.6). Sem saldo, zera tudo.
    /// </summary>
    private static (int Streak, int Consecutivos, int Saldo) Cobrir(
        StreakState state,
        DateOnly day,
        DateTimeOffset now,
        List<FreezeLedgerEntry> ledger)
    {
        if (state.FreezeBalance <= 0) return (0, 0, state.FreezeBalance);

        ledger.Add(new FreezeLedgerEntry(-1, FreezeReason.DayCovered, day, now));
        return (state.CurrentStreak + 1, 0, state.FreezeBalance - 1);
    }

    /// <summary>
    /// Crédito com teto. No teto o crédito não some em silêncio: vira linha
    /// <c>CapOverflow</c> com delta zero, para a tela "de onde vieram" poder explicar o
    /// congelamento que a pessoa ganhou e não recebeu (§3.6).
    /// </summary>
    private static int Creditar(
        int saldo,
        FreezeReason reason,
        DateOnly day,
        DateTimeOffset now,
        List<FreezeLedgerEntry> ledger)
    {
        if (saldo >= RuleConstants.FreezeStockCap)
        {
            ledger.Add(new FreezeLedgerEntry(0, FreezeReason.CapOverflow, day, now));
            return saldo;
        }

        ledger.Add(new FreezeLedgerEntry(1, reason, day, now));
        return saldo + 1;
    }
}
