using Tomatempo.Domain;

namespace Tomatempo.Domain.Accounts;

/// <summary>Os dois tokens de e-mail da conta, que têm prazos diferentes (§3.10).</summary>
public enum AccountTokenKind
{
    EmailConfirmation,
    PasswordReset,
}

/// <summary>
/// Validade dos tokens enviados por e-mail. A confirmação é folgada porque disputa com a
/// caixa de entrada; o reset é curto porque é a chave da conta
/// (REGRAS-DE-NEGOCIO.md, log de 03/09/2026).
/// </summary>
public static class AccountTokens
{
    public static TimeSpan LifetimeOf(AccountTokenKind kind) =>
        kind == AccountTokenKind.EmailConfirmation
            ? RuleConstants.EmailConfirmationTokenLifetime
            : RuleConstants.PasswordResetTokenLifetime;

    /// <summary>
    /// Vale até o instante da expiração, inclusive. Emissão no futuro não vale: o token é
    /// carimbado pelo relógio do servidor, e um instante adiantado é dado corrompido.
    /// </summary>
    public static bool IsValid(AccountTokenKind kind, DateTimeOffset issuedAt, DateTimeOffset now) =>
        now >= issuedAt && now - issuedAt <= LifetimeOf(kind);
}
