using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Tomatempo.Domain.Accounts;

namespace Tomatempo.Api.Auth;

/// <summary>
/// Implementação de produção: lê a identidade do cookie externo que o handler do Google
/// preencheu ao voltar do fluxo authorization code. O <c>email_verified</c> vem mapeado das
/// claims do provedor — é ele que decide se o acesso entra ou é recusado (§3.10).
/// </summary>
public sealed class CookieExternalIdentityReader : IExternalIdentityReader
{
    public async Task<ExternalIdentity?> ReadAsync(HttpContext context)
    {
        var resultado = await context.AuthenticateAsync(AuthSchemes.External);
        if (!resultado.Succeeded)
        {
            return null;
        }

        var principal = resultado.Principal;
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);
        if (subject is null || email is null)
        {
            return null;
        }

        var provider = resultado.Properties?.Items[".AuthScheme"] ?? "Google";
        var verificado = principal.FindFirstValue("email_verified") == "true";

        return new ExternalIdentity(provider, subject, email, verificado);
    }
}
