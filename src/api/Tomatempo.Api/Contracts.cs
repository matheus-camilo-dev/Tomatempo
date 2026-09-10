namespace Tomatempo.Api;

// Entrada: só intenção, nunca resultado. Não existe aqui campo de minuto, de pomodoro do dia,
// de streak ou de saldo — o servidor calcula tudo isso, e o que o cliente mandar a mais é
// ignorado pela desserialização (§7.3.3).

public sealed record RegisterRequest(
    string? Email,
    string? Password,
    string? Nickname,
    int Goal,
    string? TimeZone,
    string? Locale);

public sealed record CredentialsRequest(string? Email, string? Password);

public sealed record EmailRequest(string? Email);

public sealed record TokenRequest(string? Token);

public sealed record ResetPasswordRequest(string? Token, string? Password);

public sealed record OnboardingRequest(string? Nickname, int Goal, string? TimeZone, string? Locale);

public sealed record GoalRequest(int Pomodoros);

/// <summary>Senha atual, exigida para excluir conta que tenha uma (§3.10).</summary>
public sealed record DeleteAccountRequest(string? Password);

/// <summary>
/// Preferências alteráveis hoje. `avatarId` e `themePresetId` ficam de fora de propósito: os
/// catálogos que os validariam ainda não existem (§2, catálogos estáticos), e aceitar string
/// livre agora seria entrada sem whitelist entrando no banco (§7.4). Voltam junto com eles.
/// </summary>
public sealed record PreferencesRequest(
    string? Nickname,
    string? TimeZone,
    string? Locale,
    string? ColorMode,
    bool? RankingOptOut);

// Saída: projeção explícita, campo a campo. A entidade inteira nunca é devolvida (§7.7).

public sealed record GoalView(int Current, int? Upcoming, DateOnly? UpcomingEffectiveFrom);

public sealed record StreakView(int Current, int Longest);

public sealed record FreezesView(int Balance);

public sealed record MeView(
    string? Email,
    bool EmailConfirmed,
    // Booleano derivado: diz se a exclusão pede senha, nunca o hash nem parte dela (§3.10).
    bool HasPassword,
    string? Nickname,
    bool OnboardingRequired,
    string TimeZone,
    string Locale,
    string ColorMode,
    string? ThemePresetId,
    string? AvatarId,
    bool RankingOptOut,
    GoalView? Goal,
    StreakView Streak,
    FreezesView Freezes);

public sealed record FreezeEntryView(int Delta, string Reason, DateOnly? RefDate, DateTimeOffset CreatedAt);

public sealed record FreezeLedgerView(int Balance, IReadOnlyList<FreezeEntryView> Entries);

public sealed record GoalChangeView(int Pomodoros, DateOnly EffectiveFrom);

// --- Sessões de foco (§3.2, §3.3) ---------------------------------------------------------

/// <summary>
/// Único campo de entrada de `/sessions/start`. Não existe aqui `startedAt`, `duration` nem
/// `countedMinutes`: tudo isso é carimbo do servidor, e o que o cliente mandar a mais é
/// ignorado pela desserialização (§7.3.3).
/// </summary>
public sealed record StartSessionRequest(string? Kind);

public sealed record StartedSessionView(
    Guid SessionId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndsAt,
    DateTimeOffset ServerNow);

public sealed record HeartbeatView(bool Ok, DateTimeOffset ServerNow, int RemainingSeconds);

public sealed record CompletedSessionView(int CountedMinutes, int CompletedToday, int Goal, bool GoalMet);

public sealed record AbandonedSessionView(int CountedMinutes, bool Discarded);

/// <summary>
/// Um item do lote offline (§3.4). Aqui — e só aqui — o cliente manda início e duração: ele
/// estava sozinho, não tem carimbo de servidor para nenhum dos dois. Por isso cada item ainda
/// passa por janela, faixa, sobreposição, teto e idempotência antes de virar minuto, e nenhum
/// deles vale para o ranking (§7.3.3).
/// </summary>
public sealed record OfflineSessionRequest(
    string? ClientSessionId,
    DateTimeOffset StartedAt,
    int DurationSeconds);

public sealed record OfflineBatchRequest(IReadOnlyList<OfflineSessionRequest>? Items);

/// <summary>Veredito de um item. `reason` é o `code` que o cliente usa para descartar sem retry.</summary>
public sealed record OfflineItemView(string ClientSessionId, bool Accepted, string? Reason);

public sealed record OfflineBatchView(IReadOnlyList<OfflineItemView> Items);

public sealed record ActiveSessionView(
    Guid SessionId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndsAt,
    DateTimeOffset ServerNow,
    int RemainingSeconds);

// --- Estatísticas (§4) --------------------------------------------------------------------

public sealed record TodayView(
    DateOnly LocalDate,
    int CompletedPomodoros,
    int FocusMinutes,
    int RankingMinutes,
    int Goal,
    bool GoalMet);
