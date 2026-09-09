using Microsoft.EntityFrameworkCore;
using Tomatempo.Domain;
using Tomatempo.Domain.Accounts;
using Tomatempo.Domain.Sessions;
using Tomatempo.Domain.Time;
using Tomatempo.Infrastructure;
using Tomatempo.Infrastructure.Entities;

namespace Tomatempo.Api.Services;

/// <summary>Marcador de endpoint de heartbeat, lido pelo limitador de requisições (§4).</summary>
public sealed class HeartbeatEndpoint;

/// <summary>Por que uma operação de sessão não pôde acontecer.</summary>
public enum SessionFault
{
    NotFound,
    Superseded,
    Expired,
    Closed,
}

/// <summary>
/// Ciclo de vida de uma sessão de foco (§3.2, §3.3). Toda decisão de minuto vem de
/// <see cref="FocusSessionAccounting"/>; aqui mora só a persistência e a ordem das coisas.
///
/// O usuário é sempre o dono da sessão do cookie: o id nunca vem de rota, corpo ou query
/// (§7.2/A01). A rota traz o id da sessão, e sessão que não é da pessoa responde 404 — igual
/// a sessão que não existe, para não virar oráculo.
/// </summary>
public sealed class SessionWorkflow(TomatempoDbContext db, TimeProvider clock)
{
    public async Task<FocusSessionRow> StartAsync(
        TomatempoUser user,
        CancellationToken cancellationToken)
    {
        var agora = clock.GetUtcNow();

        // Sessão única (§3.3): a anterior é encerrada como `Superseded`, com o parcial dela
        // apurado normalmente — o tempo já focado não some porque a pessoa trocou de aba.
        var anterior = await AtivaAsync(user.Id, cancellationToken);
        if (anterior is not null)
        {
            await EncerrarAsync(user, anterior, SessionStatus.Superseded, agora, cancellationToken);
        }

        var sessao = new FocusSessionRow
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            StartedAtUtc = agora,
            LastHeartbeatAtUtc = agora,
            Status = SessionStatus.Active,
            Source = SessionSource.Online,
            CountedMinutes = 0,
            RankingEligible = false,
            LocalDate = LocalDay.Of(agora, FusoDe(user)),
        };

        db.FocusSessions.Add(sessao);
        await db.SaveChangesAsync(cancellationToken);

        return sessao;
    }

    public async Task<(FocusSessionRow? Session, SessionFault? Fault)> BeatAsync(
        TomatempoUser user,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var (sessao, falha) = await CarregarAsync(user.Id, sessionId, cancellationToken);
        if (sessao is null) return (null, falha);

        var agora = clock.GetUtcNow();

        // A coluna só avança enquanto a cadeia está inteira (§3.2). A batida atrasada é
        // aceita — pode ser a rede — mas não move o ponto válido, e é assim que o tempo em
        // que a aba esteve fechada não entra na conta.
        if (sessao.LastHeartbeatAtUtc is null
            || FocusSessionAccounting.ChainHolds(sessao.LastHeartbeatAtUtc.Value, agora))
        {
            sessao.LastHeartbeatAtUtc = agora;
            await db.SaveChangesAsync(cancellationToken);
        }

        return (sessao, null);
    }

    /// <summary>
    /// Encerra a sessão a pedido do cliente. Pedir conclusão não conclui nada: quem decide se
    /// o ciclo fechou é a apuração, e ela só olha carimbo de servidor (§3.2).
    /// </summary>
    public async Task<(FocusSessionRow? Session, SessionFault? Fault)> SettleAsync(
        TomatempoUser user,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var (sessao, falha) = await CarregarAsync(user.Id, sessionId, cancellationToken);
        if (sessao is null) return (null, falha);

        await EncerrarAsync(user, sessao, SessionStatus.Abandoned, clock.GetUtcNow(), cancellationToken);

        return (sessao, null);
    }

    public Task<FocusSessionRow?> AtivaAsync(Guid userId, CancellationToken cancellationToken) =>
        db.FocusSessions
            .Where(f => f.UserId == userId && f.Status == SessionStatus.Active)
            .OrderByDescending(f => f.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Apura, sela o estado e soma no dia. <paramref name="fallback"/> é o status de quem não
    /// fechou o ciclo — abandono a pedido, sessão assumida por outra aba ou varrida por falta
    /// de heartbeat.
    /// </summary>
    public async Task EncerrarAsync(
        TomatempoUser user,
        FocusSessionRow sessao,
        SessionStatus fallback,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        var apurada = FocusSessionAccounting.SettleFromLastHeartbeat(
            sessao.StartedAtUtc, sessao.LastHeartbeatAtUtc, agora, agora);

        var minutos = await DentroDoTetoAsync(sessao, apurada.CountedMinutes, cancellationToken);

        sessao.EndedAtUtc = agora;
        sessao.CountedMinutes = minutos;
        sessao.Status = apurada.Completed && minutos > 0 ? SessionStatus.Completed : fallback;
        sessao.RankingEligible =
            sessao.Source == SessionSource.Online
            && minutos >= RuleConstants.MinCountedMinutes
            && user.EmailConfirmed
            && !user.RankingOptOut;

        await SomarNoDiaAsync(user, sessao, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DailyStatsRow> DiaAsync(
        TomatempoUser user,
        DateOnly localDate,
        CancellationToken cancellationToken)
    {
        var dia = await db.DailyStats
            .FirstOrDefaultAsync(d => d.UserId == user.Id && d.LocalDate == localDate, cancellationToken);

        if (dia is not null) return dia;

        var metas = await db.UserGoals
            .Where(g => g.UserId == user.Id && g.EffectiveFrom <= localDate)
            .Select(g => new GoalEntry(g.Pomodoros, g.EffectiveFrom))
            .ToListAsync(cancellationToken);

        dia = new DailyStatsRow
        {
            UserId = user.Id,
            LocalDate = localDate,
            Goal = metas.Count == 0 ? RuleConstants.DefaultDailyGoal : GoalSchedule.InEffectOn(metas, localDate),
        };

        db.DailyStats.Add(dia);
        return dia;
    }

    public static TimeZoneInfo FusoDe(TomatempoUser user) => TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone);

    /// <summary>
    /// Teto diário de 16 sessões apuradas (§3.2). A excedente é registrada — não recusada —
    /// com zero minuto: o cliente não precisa saber que bateu no teto para continuar usando o
    /// timer, e o histórico guarda que a sessão existiu.
    /// </summary>
    private async Task<int> DentroDoTetoAsync(
        FocusSessionRow sessao,
        int minutos,
        CancellationToken cancellationToken)
    {
        if (minutos == 0) return 0;

        var apuradas = await db.FocusSessions.CountAsync(
            f => f.UserId == sessao.UserId
                && f.LocalDate == sessao.LocalDate
                && f.Id != sessao.Id
                && f.CountedMinutes > 0,
            cancellationToken);

        return apuradas >= RuleConstants.MaxSessionsPerLocalDay ? 0 : minutos;
    }

    private async Task SomarNoDiaAsync(
        TomatempoUser user,
        FocusSessionRow sessao,
        CancellationToken cancellationToken)
    {
        var dia = await DiaAsync(user, sessao.LocalDate, cancellationToken);

        // Dia já selado não muda mais (§3.6): sessão que chega depois do fechamento pertence
        // ao histórico, não à streak que já foi decidida.
        if (dia.ClosedAt is not null) return;

        dia.FocusMinutes += sessao.CountedMinutes;
        if (sessao.RankingEligible) dia.OnlineFocusMinutes += sessao.CountedMinutes;
        if (sessao.Status == SessionStatus.Completed) dia.CompletedPomodoros++;

        dia.GoalMet = dia.CompletedPomodoros >= dia.Goal;
    }

    private async Task<(FocusSessionRow? Session, SessionFault? Fault)> CarregarAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var sessao = await db.FocusSessions
            .FirstOrDefaultAsync(f => f.Id == sessionId && f.UserId == userId, cancellationToken);

        if (sessao is null) return (null, SessionFault.NotFound);

        return sessao.Status switch
        {
            SessionStatus.Active => (sessao, null),
            SessionStatus.Superseded => (null, SessionFault.Superseded),
            SessionStatus.Expired => (null, SessionFault.Expired),
            _ => (null, SessionFault.Closed),
        };
    }
}
