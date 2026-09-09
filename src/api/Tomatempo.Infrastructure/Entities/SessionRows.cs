using Tomatempo.Domain.Sessions;

namespace Tomatempo.Infrastructure.Entities;

/// <summary>De onde a sessão veio (§2, `focus_sessions`).</summary>
public enum SessionSource
{
    Online,
    Offline,
}

/// <summary>
/// Linha de `focus_sessions` (§2). Pausas não entram aqui: não pontuam e não alimentam
/// regra alguma.
/// </summary>
public sealed class FocusSessionRow
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Relógio do servidor quando online; início declarado pelo cliente quando offline.</summary>
    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? EndedAtUtc { get; set; }

    public DateTimeOffset? LastHeartbeatAtUtc { get; set; }

    public SessionStatus Status { get; set; }

    public SessionSource Source { get; set; }

    /// <summary>Minutos válidos apurados; zero quando descartada ou acima do teto do dia.</summary>
    public int CountedMinutes { get; set; }

    /// <summary><c>Online &amp;&amp; counted &gt;= 5 &amp;&amp; email_confirmed &amp;&amp; !opt_out</c> (§2).</summary>
    public bool RankingEligible { get; set; }

    /// <summary>Dia local do usuário no início da sessão — é por ele que meta e teto contam.</summary>
    public DateOnly LocalDate { get; set; }

    /// <summary>Idempotência do lote offline, único por usuário (§3.4).</summary>
    public string? ClientSessionId { get; set; }
}

/// <summary>
/// Linha de `daily_stats` (§2): uma por usuário e dia local, atualizada a cada sessão apurada
/// e selada pelo job de fechamento.
/// </summary>
public sealed class DailyStatsRow
{
    public Guid UserId { get; set; }

    public DateOnly LocalDate { get; set; }

    public int CompletedPomodoros { get; set; }

    public int FocusMinutes { get; set; }

    /// <summary>Subconjunto de <see cref="FocusMinutes"/> que vale para o ranking (§3.4).</summary>
    public int OnlineFocusMinutes { get; set; }

    public int Goal { get; set; }

    public bool GoalMet { get; set; }

    public bool FreezeUsed { get; set; }

    /// <summary>Selo do fechamento; enquanto for nulo, o dia ainda pode mudar (§3.6).</summary>
    public DateTimeOffset? ClosedAt { get; set; }
}
