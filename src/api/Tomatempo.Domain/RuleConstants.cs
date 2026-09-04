namespace Tomatempo.Domain;

/// <summary>
/// Todo número que é regra mora aqui, com a seção que o justifica ao lado (§8.4).
/// Trocar um deles obriga a tocar, no mesmo commit, a constante, a linha do documento de
/// regras e o teste que a exercita — é o que impede um limite antifraude de mudar de valor
/// sem ninguém perceber.
/// </summary>
public static class RuleConstants
{
    // --- Apelido (§3.10, §11.8) ---------------------------------------------
    public const int NicknameMinLength = 3;
    public const int NicknameMaxLength = 20;
    public const int NicknameChangeLockDays = 30;

    // --- Meta diária (§3.5, §2 `user_goals`) --------------------------------
    /// <summary>Cardápio fechado do cadastro.</summary>
    public static readonly IReadOnlyList<int> AllowedSignupGoals = [2, 4, 6, 8];

    public const int DefaultDailyGoal = 4;
    public const int MinDailyGoal = 1;
    public const int MaxDailyGoal = 24;

    // --- Congelamentos (§3.10, §3.6) ----------------------------------------
    public const int SignupFreezeGrant = 2;
    public const int FreezeStockCap = 5;

    // --- Tokens de e-mail (§3.10) -------------------------------------------
    public static readonly TimeSpan EmailConfirmationTokenLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    // --- Senha e rate limit (§4, §7.2/A07) ----------------------------------
    public const int PasswordMinLength = 8;
    public const int AuthAttemptsPerMinute = 5;
}
