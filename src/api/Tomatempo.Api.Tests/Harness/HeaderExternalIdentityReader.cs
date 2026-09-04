using Microsoft.AspNetCore.Http;
using Tomatempo.Api.Auth;
using Tomatempo.Domain.Accounts;

namespace Tomatempo.Api.Tests.Harness;

/// <summary>
/// Dublê do provedor OAuth (§6.4): a identidade externa chega por cabeçalho, em vez de vir
/// do cookie que o handler do Google preencheria. Nada disso existe em produção — o teste
/// exercita o mesmo endpoint de callback, só sem a viagem até o Google.
/// </summary>
public sealed class HeaderExternalIdentityReader : IExternalIdentityReader
{
    public const string SubjectHeader = "X-Teste-Externo-Subject";
    public const string EmailHeader = "X-Teste-Externo-Email";
    public const string EmailVerifiedHeader = "X-Teste-Externo-EmailVerificado";

    public Task<ExternalIdentity?> ReadAsync(HttpContext context)
    {
        var headers = context.Request.Headers;
        if (!headers.TryGetValue(SubjectHeader, out var subject) || !headers.TryGetValue(EmailHeader, out var email))
        {
            return Task.FromResult<ExternalIdentity?>(null);
        }

        var verified = headers.TryGetValue(EmailVerifiedHeader, out var raw) && raw.ToString() == "true";
        return Task.FromResult<ExternalIdentity?>(
            new ExternalIdentity("Google", subject.ToString(), email.ToString(), verified));
    }
}
