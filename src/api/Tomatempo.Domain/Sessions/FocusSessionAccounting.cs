namespace Tomatempo.Domain.Sessions;

/// <summary>Em que estado uma sessão de foco terminou (§2, `focus_sessions`).</summary>
public enum SessionStatus
{
    Active,
    Completed,
    Abandoned,
    Expired,
    Superseded,
}

/// <summary>
/// O que uma sessão rendeu. <c>Completed</c> é o que conta para a meta diária; os minutos
/// contam para foco e ranking mesmo quando a sessão não fechou o ciclo (§3.5).
/// </summary>
public sealed record SessionSettlement(int CountedMinutes, bool Completed);

/// <summary>
/// Apuração dos minutos de uma sessão online (§3.2).
///
/// Função pura sobre carimbos do servidor. O cliente não manda minuto, duração nem hora —
/// só bate heartbeat, e o servidor carimba cada batida (§7.3.3). É por isso que adiantar o
/// relógio da máquina não vira minuto: o corte é sempre o instante do servidor.
/// </summary>
public static class FocusSessionAccounting
{
    public static SessionSettlement Settle(
        DateTimeOffset startedAt,
        IReadOnlyList<DateTimeOffset> heartbeats,
        DateTimeOffset endedAt,
        DateTimeOffset serverNow)
    {
        var fim = endedAt < serverNow ? endedAt : serverNow;
        var ultimoValido = UltimoPontoDaCadeia(startedAt, heartbeats, fim);

        var decorrido = ultimoValido - startedAt;
        if (decorrido > RuleConstants.FocusDuration) decorrido = RuleConstants.FocusDuration;

        var minutos = decorrido < TimeSpan.Zero ? 0 : (int)decorrido.TotalMinutes;
        if (minutos < RuleConstants.MinCountedMinutes) minutos = 0;

        var concluida =
            minutos >= (int)RuleConstants.FocusDuration.TotalMinutes
            && fim - ultimoValido <= RuleConstants.MaxHeartbeatGap;

        return new SessionSettlement(minutos, concluida);
    }

    /// <summary>
    /// Até onde a cadeia de heartbeats prova que alguém estava lá. O primeiro buraco maior
    /// que <see cref="RuleConstants.MaxHeartbeatGap"/> corta — inclusive o buraco entre o
    /// último batimento e o fim, que é o caso da aba fechada sem avisar: sem esse corte, o
    /// varredor creditaria os minutos que ninguém passou focando (§3.2).
    /// </summary>
    private static DateTimeOffset UltimoPontoDaCadeia(
        DateTimeOffset startedAt,
        IReadOnlyList<DateTimeOffset> heartbeats,
        DateTimeOffset fim)
    {
        var anterior = startedAt;

        // Ordenado porque heartbeat chega fora de ordem: a rede não garante sequência, e a
        // cadeia é cronológica por definição.
        foreach (var batida in heartbeats.Where(b => b <= fim).OrderBy(b => b))
        {
            if (batida - anterior > RuleConstants.MaxHeartbeatGap) return anterior;

            anterior = batida;
        }

        return fim - anterior > RuleConstants.MaxHeartbeatGap ? anterior : fim;
    }
}
