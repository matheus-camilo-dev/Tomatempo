using Microsoft.EntityFrameworkCore;
using Tomatempo.Domain;
using Tomatempo.Domain.Accounts;
using Tomatempo.Domain.Freezes;
using Tomatempo.Domain.Streaks;
using Tomatempo.Domain.Time;
using Tomatempo.Infrastructure;
using Tomatempo.Infrastructure.Entities;

namespace Tomatempo.Api.Services;

/// <summary>
/// Fechamento do dia local (§3.6). A regra mora em <see cref="DayClosing"/>; aqui está quem
/// fecha, quando, e o que fica selado.
///
/// Serviço, e não `BackgroundService`: quem acorda de hora em hora é
/// <see cref="DailyCloserService"/>. Separar deixa o teste de contrato fechar o dia no
/// instante que ele escolher, em vez de esperar o relógio.
/// </summary>
public sealed class DailyCloser(TomatempoDbContext db, TimeProvider clock)
{
    /// <summary>Fecha todo dia local que já terminou e ainda não foi selado. Devolve quantos.</summary>
    public async Task<int> CloseDueAsync(CancellationToken cancellationToken)
    {
        var agora = clock.GetUtcNow();
        var fechados = 0;

        // Só quem terminou o onboarding tem meta, e sem meta não há dia a fechar (§3.10).
        var contas = await db.Users
            .Where(u => u.OnboardingCompletedAt != null)
            .Select(u => new { u.Id, u.TimeZone, u.CreatedAt })
            .ToListAsync(cancellationToken);

        foreach (var conta in contas)
        {
            fechados += await FecharDaContaAsync(conta.Id, conta.TimeZone, conta.CreatedAt, agora, cancellationToken);
        }

        if (fechados > 0) await db.SaveChangesAsync(cancellationToken);

        return fechados;
    }

    private async Task<int> FecharDaContaAsync(
        Guid userId,
        string timeZone,
        DateTimeOffset createdAt,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        var fuso = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var streak = await db.UserStreaks.FirstAsync(s => s.UserId == userId, cancellationToken);

        // Conta nova nunca fechou nada: o primeiro dia a fechar é o da criação, não o começo
        // dos tempos — senão o job varreria anos de dias vazios no primeiro fechamento.
        var ultimoFechado = streak.LastClosedLocalDate ?? LocalDay.Of(createdAt, fuso).AddDays(-1);
        var pendentes = LocalDay.PendingClosures(ultimoFechado, agora, fuso);
        if (pendentes.Count == 0) return 0;

        var metas = await db.UserGoals
            .Where(g => g.UserId == userId)
            .Select(g => new GoalEntry(g.Pomodoros, g.EffectiveFrom))
            .ToListAsync(cancellationToken);

        foreach (var dia in pendentes)
        {
            await FecharDiaAsync(userId, dia, metas, streak, agora, cancellationToken);
        }

        streak.LastClosedLocalDate = pendentes[^1];

        return pendentes.Count;
    }

    private async Task FecharDiaAsync(
        Guid userId,
        DateOnly dia,
        IReadOnlyCollection<GoalEntry> metas,
        UserStreakRow streak,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        var linha = await db.DailyStats
            .FirstOrDefaultAsync(d => d.UserId == userId && d.LocalDate == dia, cancellationToken);

        // Dia sem atividade alguma não tem linha, e mesmo assim fecha: dia parado consome
        // congelamento igual (REGRAS-DE-NEGOCIO.md §4).
        if (linha is null)
        {
            linha = new DailyStatsRow
            {
                UserId = userId,
                LocalDate = dia,
                Goal = MetaDe(metas, dia),
            };

            db.DailyStats.Add(linha);
        }

        // O dia é cobrado contra a meta que valia nele, e não contra a que vale agora: é o que
        // impede baixar a meta às 23h para salvar a streak (§3.5).
        linha.GoalMet = linha.CompletedPomodoros >= linha.Goal;

        var antes = new StreakState(
            streak.CurrentStreak, streak.LongestStreak, streak.ConsecutiveGoalDays, streak.FreezeBalance);

        var fechado = DayClosing.Close(antes, linha.GoalMet, dia, agora);

        streak.CurrentStreak = fechado.State.CurrentStreak;
        streak.LongestStreak = fechado.State.LongestStreak;
        streak.ConsecutiveGoalDays = fechado.State.ConsecutiveGoalDays;
        streak.FreezeBalance = fechado.State.FreezeBalance;

        linha.FreezeUsed = fechado.Ledger.Any(l => l.Reason == FreezeReason.DayCovered);
        linha.ClosedAt = agora;

        foreach (var entrada in fechado.Ledger)
        {
            db.FreezeLedger.Add(new FreezeLedgerRow
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                Delta = entrada.Delta,
                Reason = entrada.Reason,
                RefDate = entrada.RefDate,
                CreatedAt = entrada.CreatedAt,
            });
        }
    }

    private static int MetaDe(IReadOnlyCollection<GoalEntry> metas, DateOnly dia)
    {
        var vigentes = metas.Where(m => m.EffectiveFrom <= dia).ToList();

        return vigentes.Count == 0 ? RuleConstants.DefaultDailyGoal : GoalSchedule.InEffectOn(vigentes, dia);
    }
}
