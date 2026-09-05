using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tomatempo.Infrastructure.Entities;

namespace Tomatempo.Infrastructure;

/// <summary>
/// Modelo do banco. Nomes em snake_case como a §2 documenta; as tabelas do Identity ficam com
/// os nomes dele, que é o que a §2 já assume ao dizer "estende AspNetUsers".
/// </summary>
public sealed class TomatempoDbContext(DbContextOptions<TomatempoDbContext> options)
    : IdentityDbContext<TomatempoUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<UserGoalRow> UserGoals => Set<UserGoalRow>();

    public DbSet<FreezeLedgerRow> FreezeLedger => Set<FreezeLedgerRow>();

    public DbSet<UserStreakRow> UserStreaks => Set<UserStreakRow>();

    public DbSet<AccountTokenRow> AccountTokens => Set<AccountTokenRow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // `citext` é o que faz `ANA_1` e `ana_1` serem o mesmo apelido no índice único —
        // a comparação sem caixa vive no banco, não em código que alguém pode esquecer.
        builder.HasPostgresExtension("citext");

        builder.Entity<TomatempoUser>(user =>
        {
            user.Property(u => u.Nickname).HasColumnName("nickname").HasColumnType("citext");
            user.Property(u => u.AvatarId).HasColumnName("avatar_id");
            user.Property(u => u.TimeZone).HasColumnName("time_zone").IsRequired();
            user.Property(u => u.Locale).HasColumnName("locale").IsRequired();
            user.Property(u => u.ColorMode).HasColumnName("color_mode").IsRequired();
            user.Property(u => u.ThemePresetId).HasColumnName("theme_preset_id");
            user.Property(u => u.RankingOptOut).HasColumnName("ranking_opt_out");
            user.Property(u => u.NicknameChangedAt).HasColumnName("nickname_changed_at");
            user.Property(u => u.OnboardingCompletedAt).HasColumnName("onboarding_completed_at");
            user.Property(u => u.CreatedAt).HasColumnName("created_at");
            user.HasIndex(u => u.Nickname).IsUnique();
        });

        builder.Entity<UserGoalRow>(goal =>
        {
            goal.ToTable("user_goals");
            goal.HasKey(g => g.Id);
            goal.Property(g => g.Id).HasColumnName("id");
            goal.Property(g => g.UserId).HasColumnName("user_id");
            goal.Property(g => g.Pomodoros).HasColumnName("pomodoros");
            goal.Property(g => g.EffectiveFrom).HasColumnName("effective_from");
            goal.Property(g => g.CreatedAt).HasColumnName("created_at");
            goal.HasIndex(g => new { g.UserId, g.EffectiveFrom }).IsUnique();
            goal.HasOne<TomatempoUser>().WithMany().HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FreezeLedgerRow>(entry =>
        {
            entry.ToTable("freeze_ledger");
            entry.HasKey(e => e.Id);
            entry.Property(e => e.Id).HasColumnName("id");
            entry.Property(e => e.UserId).HasColumnName("user_id");
            entry.Property(e => e.Delta).HasColumnName("delta");
            entry.Property(e => e.Reason).HasColumnName("reason").HasConversion<string>();
            entry.Property(e => e.RefDate).HasColumnName("ref_date");
            entry.Property(e => e.CreatedAt).HasColumnName("created_at");
            entry.HasIndex(e => new { e.UserId, e.CreatedAt });
            entry.HasOne<TomatempoUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserStreakRow>(streak =>
        {
            streak.ToTable("user_streaks");
            streak.HasKey(s => s.UserId);
            streak.Property(s => s.UserId).HasColumnName("user_id");
            streak.Property(s => s.CurrentStreak).HasColumnName("current_streak");
            streak.Property(s => s.LongestStreak).HasColumnName("longest_streak");
            streak.Property(s => s.LastClosedLocalDate).HasColumnName("last_closed_local_date");
            streak.Property(s => s.FreezeBalance).HasColumnName("freeze_balance");
            streak.Property(s => s.ConsecutiveGoalDays).HasColumnName("consecutive_goal_days");
            streak.HasOne<TomatempoUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AccountTokenRow>(token =>
        {
            token.ToTable("account_tokens");
            token.HasKey(t => t.Id);
            token.Property(t => t.Id).HasColumnName("id");
            token.Property(t => t.UserId).HasColumnName("user_id");
            token.Property(t => t.Kind).HasColumnName("kind").HasConversion<string>();
            token.Property(t => t.TokenHash).HasColumnName("token_hash").IsRequired();
            token.Property(t => t.IssuedAt).HasColumnName("issued_at");
            token.Property(t => t.ConsumedAt).HasColumnName("consumed_at");
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasOne<TomatempoUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
