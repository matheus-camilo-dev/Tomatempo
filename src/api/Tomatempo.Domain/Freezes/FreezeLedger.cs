namespace Tomatempo.Domain.Freezes;

/// <summary>De onde veio (ou para onde foi) cada congelamento (§2, `freeze_ledger`).</summary>
public enum FreezeReason
{
    Signup,
    StreakMilestone,
    WeeklyRanking,
    DayCovered,
    CapOverflow,
}

/// <summary>
/// Uma linha do ledger. O saldo do usuário é sempre reconciliável somando os deltas —
/// é o que permite a tela "de onde vieram meus congelamentos" (§2).
/// </summary>
public sealed record FreezeLedgerEntry(int Delta, FreezeReason Reason, DateOnly? RefDate, DateTimeOffset CreatedAt);

/// <summary>Créditos de congelamento que não dependem de fechamento de dia nem de semana.</summary>
public static class FreezeGrants
{
    /// <summary>O crédito que toda conta nova recebe (§3.10).</summary>
    public static FreezeLedgerEntry ForSignup(DateTimeOffset now) => throw new NotImplementedException();
}
