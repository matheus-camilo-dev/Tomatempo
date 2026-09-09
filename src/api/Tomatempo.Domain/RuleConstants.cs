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

    // --- Sessão de foco (§3.1, §3.2) ----------------------------------------
    /// <summary>O ciclo é fixo: ficar com a aba aberta além disso não rende minuto.</summary>
    public static readonly TimeSpan FocusDuration = TimeSpan.FromMinutes(25);

    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    /// <summary>Buraco maior que isto na cadeia trunca a apuração no último batimento.</summary>
    public static readonly TimeSpan MaxHeartbeatGap = TimeSpan.FromSeconds(90);

    /// <summary>Sem batimento por mais que isto, o varredor encerra a sessão como `Expired`.</summary>
    public static readonly TimeSpan ScavengerIdleLimit = TimeSpan.FromMinutes(2);

    /// <summary>Abaixo disso o parcial é descartado inteiro.</summary>
    public const int MinCountedMinutes = 5;

    /// <summary>Teto antifraude por dia local, somando online e offline.</summary>
    public const int MaxSessionsPerLocalDay = 16;

    // --- Sincronização offline (§3.4) ---------------------------------------
    public static readonly TimeSpan OfflineMaxAge = TimeSpan.FromHours(48);

    /// <summary>Folga para desencontro de relógio, não para relógio manipulado.</summary>
    public static readonly TimeSpan OfflineClockTolerance = TimeSpan.FromMinutes(2);

    // --- Streak (§3.6) ------------------------------------------------------
    /// <summary>A cada tantos dias consecutivos de meta batida, +1 congelamento.</summary>
    public const int FreezeMilestoneDays = 7;

    // --- Congelamentos (§3.10, §3.6) ----------------------------------------
    public const int SignupFreezeGrant = 2;
    public const int FreezeStockCap = 5;

    // --- Tokens de e-mail (§3.10) -------------------------------------------
    public static readonly TimeSpan EmailConfirmationTokenLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    // --- Senha, e-mail e rate limit (§4, §7.2/A07, §3.10) -------------------
    public const int PasswordMinLength = 8;

    /// <summary>Teto da senha: ela alimenta um hash caro de propósito (Argon2id).</summary>
    public const int PasswordMaxLength = 128;

    /// <summary>Limite de endereço da RFC 5321, e o tamanho da coluna.</summary>
    public const int EmailMaxLength = 254;

    public const int AuthAttemptsPerMinute = 5;

    /// <summary>Teto por IP nos endpoints anônimos de autenticação.</summary>
    public const int AnonymousAuthAttemptsPerMinutePerIp = 60;

    /// <summary>Heartbeat bate a cada 30s, então 60/min deixa folga para reenvio (§4).</summary>
    public const int HeartbeatsPerMinute = 60;

    /// <summary>Teto por conta em todo o resto (§4).</summary>
    public const int AccountRequestsPerMinute = 20;
}
