namespace Tomatempo.Domain.Sessions;

/// <summary>Uma sessão que o cliente guardou offline e mandou no lote ao reconectar (§3.4).</summary>
public sealed record OfflineSession(string ClientSessionId, DateTimeOffset StartedAt, TimeSpan Duration);

/// <summary>Intervalo ocupado por uma sessão já registrada; serve para detectar sobreposição.</summary>
public sealed record TimeRange(DateTimeOffset Start, DateTimeOffset End);

/// <summary>Por que uma sessão do lote não entrou. O cliente descarta sem tentar de novo (§3.4).</summary>
public enum OfflineRejection
{
    TooOld,
    InFuture,
    InvalidDuration,
    Overlaps,
    DailyCapReached,
    Duplicate,
}

/// <summary>
/// Veredito de uma sessão do lote. <c>RankingEligible</c> é sempre falso: minuto de ranking
/// exige começo e fim validados pelo servidor, e offline não tem nem um nem outro (§3.4).
/// </summary>
public sealed record OfflineAdmission(
    bool Accepted,
    OfflineRejection? Rejection,
    int CountedMinutes,
    bool RankingEligible);

/// <summary>
/// Aceitação do lote offline (§3.4). Nada que o cliente manda decide regra sozinho: início,
/// duração e identificador passam por janela, faixa, sobreposição e teto antes de virar
/// minuto (§7.3.3).
/// </summary>
public static class OfflineIntake
{
    private static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(RuleConstants.MinCountedMinutes);

    public static OfflineAdmission Judge(
        OfflineSession session,
        DateTimeOffset serverNow,
        IReadOnlyList<TimeRange> alreadyRecorded,
        int sessionsOnLocalDay,
        IReadOnlySet<string> knownClientIds)
    {
        var recusa = Recusa(session, serverNow, alreadyRecorded, sessionsOnLocalDay, knownClientIds);
        if (recusa is not null) return new OfflineAdmission(false, recusa, 0, RankingEligible: false);

        return new OfflineAdmission(
            Accepted: true,
            Rejection: null,
            CountedMinutes: (int)session.Duration.TotalMinutes,
            RankingEligible: false);
    }

    private static OfflineRejection? Recusa(
        OfflineSession session,
        DateTimeOffset serverNow,
        IReadOnlyList<TimeRange> alreadyRecorded,
        int sessionsOnLocalDay,
        IReadOnlySet<string> knownClientIds)
    {
        // Duplicata antes de tudo: reenviar o mesmo lote é o caso comum quando a rede cai no
        // meio do envio, e precisa ser no-op barato, não erro.
        if (knownClientIds.Contains(session.ClientSessionId)) return OfflineRejection.Duplicate;

        if (session.StartedAt > serverNow + RuleConstants.OfflineClockTolerance)
        {
            return OfflineRejection.InFuture;
        }

        if (session.StartedAt < serverNow - RuleConstants.OfflineMaxAge) return OfflineRejection.TooOld;

        if (session.Duration < MinDuration || session.Duration > RuleConstants.FocusDuration)
        {
            return OfflineRejection.InvalidDuration;
        }

        if (sessionsOnLocalDay >= RuleConstants.MaxSessionsPerLocalDay)
        {
            return OfflineRejection.DailyCapReached;
        }

        var fim = session.StartedAt + session.Duration;

        // Encostar não é sobrepor: terminar às 14h e começar às 14h é sequência normal.
        if (alreadyRecorded.Any(ocupado => session.StartedAt < ocupado.End && fim > ocupado.Start))
        {
            return OfflineRejection.Overlaps;
        }

        return null;
    }
}
