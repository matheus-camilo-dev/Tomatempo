using Tomatempo.Domain.Accounts;
using Tomatempo.Domain.Freezes;

namespace Tomatempo.Infrastructure.Entities;

/// <summary>Linha de `user_goals` (§2): a meta e o dia local em que passa a valer.</summary>
public sealed class UserGoalRow
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public int Pomodoros { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Linha de `freeze_ledger` (§2): o saldo é sempre a soma dos deltas.</summary>
public sealed class FreezeLedgerRow
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public int Delta { get; set; }

    public FreezeReason Reason { get; set; }

    public DateOnly? RefDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Linha de `user_streaks` (§2), 1:1 com a conta.</summary>
public sealed class UserStreakRow
{
    public Guid UserId { get; set; }

    public int CurrentStreak { get; set; }

    public int LongestStreak { get; set; }

    public DateOnly? LastClosedLocalDate { get; set; }

    public int FreezeBalance { get; set; }

    public int ConsecutiveGoalDays { get; set; }
}

/// <summary>
/// Token de e-mail (§3.10). Guarda só o hash: quem lê o banco não consegue confirmar conta
/// alheia nem trocar senha de ninguém (§7.2/A02). <see cref="ConsumedAt"/> é o uso único.
/// </summary>
public sealed class AccountTokenRow
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public AccountTokenKind Kind { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}
