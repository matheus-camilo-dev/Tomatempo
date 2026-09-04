using Microsoft.AspNetCore.Identity;

namespace Tomatempo.Infrastructure.Entities;

/// <summary>
/// A conta (§2, `users` estendendo AspNetUsers). Só guarda o que o produto declarou coletar:
/// e-mail, apelido, fuso e idioma (§7.7) — nada além disso.
/// </summary>
public sealed class TomatempoUser : IdentityUser<Guid>
{
    /// <summary><c>null</c> enquanto a conta criada pelo Google não passou pelo onboarding.</summary>
    public string? Nickname { get; set; }

    public string? AvatarId { get; set; }

    /// <summary>Fuso IANA. Governa o "dia" da meta e da streak (§3.6).</summary>
    public string TimeZone { get; set; } = "UTC";

    public string Locale { get; set; } = "pt-BR";

    public string ColorMode { get; set; } = "system";

    public string? ThemePresetId { get; set; }

    public bool RankingOptOut { get; set; }

    /// <summary>Última troca de apelido — base da trava de 30 dias (§11.8).</summary>
    public DateTimeOffset? NicknameChangedAt { get; set; }

    /// <summary><c>null</c> = conta incompleta: existe, mas não pontua (§3.10).</summary>
    public DateTimeOffset? OnboardingCompletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
