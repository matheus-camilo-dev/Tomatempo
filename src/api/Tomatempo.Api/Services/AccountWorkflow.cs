using Microsoft.EntityFrameworkCore;
using Tomatempo.Domain.Accounts;
using Tomatempo.Domain.Freezes;
using Tomatempo.Infrastructure;
using Tomatempo.Infrastructure.Entities;

namespace Tomatempo.Api.Services;

/// <summary>
/// O que cadastro por senha, entrada pelo Google e onboarding têm em comum: validar o que o
/// usuário escolhe, semear a conta zerada e montar a visão do próprio perfil.
///
/// Toda consulta parte do <c>userId</c> da sessão, que chega como parâmetro obrigatório — não
/// existe leitura por id vindo do cliente (§7.2/A01, §7.3.1).
/// </summary>
public sealed class AccountWorkflow(TomatempoDbContext db, TimeProvider clock)
{
    private static readonly string[] IdiomasSuportados = ["pt-BR", "en"];

    private static readonly string[] ModosDeCor = ["light", "dark", "system"];

    /// <summary>Dia local do usuário. É o fuso dele que define "hoje" (§3.6).</summary>
    public DateOnly LocalToday(TomatempoUser user)
    {
        var fuso = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), fuso).DateTime);
    }

    public static bool FusoValido(string? timeZone) =>
        !string.IsNullOrWhiteSpace(timeZone) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _);

    public static bool IdiomaValido(string? locale) => locale is not null && IdiomasSuportados.Contains(locale);

    public static bool ModoDeCorValido(string? colorMode) => colorMode is not null && ModosDeCor.Contains(colorMode);

    public Task<bool> ApelidoEmUsoAsync(string apelido, Guid? exceto, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Nickname == apelido && (exceto == null || u.Id != exceto), cancellationToken);

    /// <summary>
    /// Estado inicial de toda conta, venha ela do cadastro por senha ou do Google: streak
    /// zerada e os 2 congelamentos do cadastro no ledger (§3.10). A meta entra junto quando
    /// o usuário já a escolheu; a conta do Google só a recebe no onboarding.
    /// </summary>
    public async Task SemearContaAsync(TomatempoUser user, int? meta, CancellationToken cancellationToken)
    {
        var agora = clock.GetUtcNow();
        var credito = FreezeGrants.ForSignup(agora);

        db.FreezeLedger.Add(new FreezeLedgerRow
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Delta = credito.Delta,
            Reason = credito.Reason,
            RefDate = credito.RefDate,
            CreatedAt = credito.CreatedAt,
        });

        db.UserStreaks.Add(new UserStreakRow
        {
            UserId = user.Id,
            CurrentStreak = 0,
            LongestStreak = 0,
            LastClosedLocalDate = null,
            FreezeBalance = credito.Delta,
            ConsecutiveGoalDays = 0,
        });

        if (meta is not null)
        {
            RegistrarMetaInicial(user, meta.Value);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// A primeira meta vale desde hoje — a regra do dia seguinte protege *alterações*, e
    /// aplicá-la ao cadastro deixaria o primeiro dia do usuário sem meta nenhuma (§3.10).
    /// </summary>
    public void RegistrarMetaInicial(TomatempoUser user, int meta) =>
        db.UserGoals.Add(new UserGoalRow
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Pomodoros = meta,
            EffectiveFrom = LocalToday(user),
            CreatedAt = clock.GetUtcNow(),
        });

    /// <summary>Aplica a edição de meta e reconcilia as linhas do banco com o que a regra decidiu.</summary>
    public async Task<GoalChangeView> AlterarMetaAsync(TomatempoUser user, int pomodoros, CancellationToken cancellationToken)
    {
        var hoje = LocalToday(user);
        var linhas = await db.UserGoals.Where(g => g.UserId == user.Id).ToListAsync(cancellationToken);
        var historico = linhas.Select(l => new GoalEntry(l.Pomodoros, l.EffectiveFrom)).ToList();

        var desejado = GoalSchedule.ApplyChange(historico, pomodoros, hoje);

        foreach (var linha in linhas.Where(l => desejado.All(e => e.EffectiveFrom != l.EffectiveFrom)))
        {
            db.UserGoals.Remove(linha);
        }

        foreach (var entrada in desejado)
        {
            var linha = linhas.FirstOrDefault(l => l.EffectiveFrom == entrada.EffectiveFrom);
            if (linha is null)
            {
                db.UserGoals.Add(new UserGoalRow
                {
                    Id = Guid.CreateVersion7(),
                    UserId = user.Id,
                    Pomodoros = entrada.Pomodoros,
                    EffectiveFrom = entrada.EffectiveFrom,
                    CreatedAt = clock.GetUtcNow(),
                });
            }
            else
            {
                linha.Pomodoros = entrada.Pomodoros;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new GoalChangeView(pomodoros, GoalSchedule.EffectiveFromForChange(hoje));
    }

    public async Task<MeView> MontarPerfilAsync(TomatempoUser user, CancellationToken cancellationToken)
    {
        var streak = await db.UserStreaks.SingleAsync(s => s.UserId == user.Id, cancellationToken);
        var metas = await db.UserGoals
            .Where(g => g.UserId == user.Id)
            .OrderBy(g => g.EffectiveFrom)
            .ToListAsync(cancellationToken);

        GoalView? meta = null;
        if (metas.Count > 0)
        {
            var hoje = LocalToday(user);
            var historico = metas.Select(m => new GoalEntry(m.Pomodoros, m.EffectiveFrom)).ToList();
            var futura = metas.LastOrDefault(m => m.EffectiveFrom > hoje);
            meta = new GoalView(GoalSchedule.InEffectOn(historico, hoje), futura?.Pomodoros, futura?.EffectiveFrom);
        }

        return new MeView(
            user.Email,
            user.EmailConfirmed,
            // Conta criada pelo Google nasce sem senha; a tela de exclusão precisa
            // saber disso para não pedir uma que não existe (§3.10).
            user.PasswordHash is not null,
            user.Nickname,
            user.OnboardingCompletedAt is null,
            user.TimeZone,
            user.Locale,
            user.ColorMode,
            user.ThemePresetId,
            user.AvatarId,
            user.RankingOptOut,
            meta,
            new StreakView(streak.CurrentStreak, streak.LongestStreak),
            new FreezesView(streak.FreezeBalance));
    }

    public async Task<FreezeLedgerView> MontarCongelamentosAsync(TomatempoUser user, CancellationToken cancellationToken)
    {
        var streak = await db.UserStreaks.SingleAsync(s => s.UserId == user.Id, cancellationToken);
        var linhas = await db.FreezeLedger
            .Where(f => f.UserId == user.Id)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        var entradas = linhas
            .Select(l => new FreezeEntryView(l.Delta, l.Reason.ToString(), l.RefDate, l.CreatedAt))
            .ToList();

        return new FreezeLedgerView(streak.FreezeBalance, entradas);
    }
}
