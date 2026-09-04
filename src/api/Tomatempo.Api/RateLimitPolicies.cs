namespace Tomatempo.Api;

public static class RateLimitPolicies
{
    /// <summary>
    /// Endpoints de autenticação abertos ao público. O limite é por IP porque confirmação e
    /// reset não têm e-mail no corpo para particionar por conta — e são justamente as portas
    /// que aceitariam tentativa de token sem fim (§4).
    /// </summary>
    public const string AnonymousAuth = "auth-anonima";
}
