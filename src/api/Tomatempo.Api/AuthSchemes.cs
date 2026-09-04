using Microsoft.AspNetCore.Identity;

namespace Tomatempo.Api;

/// <summary>
/// Nomes dos esquemas de autenticação. O esquema externo é um cookie de vida curta onde o
/// handler do Google deposita a identidade antes de <c>/api/auth/google/callback</c> decidir
/// vincular ou criar a conta (§3.10) — é também o ponto onde o teste de contrato injeta uma
/// identidade externa sem falar com o Google.
/// </summary>
public static class AuthSchemes
{
    public static readonly string Application = IdentityConstants.ApplicationScheme;

    public static readonly string External = IdentityConstants.ExternalScheme;

    /// <summary>Nome do cookie de sessão — sem prefixo de framework, para não anunciar a stack.</summary>
    public const string SessionCookieName = "tomatempo_sessao";
}
