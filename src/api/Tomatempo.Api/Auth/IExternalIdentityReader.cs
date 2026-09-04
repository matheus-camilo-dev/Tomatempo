using Tomatempo.Domain.Accounts;

namespace Tomatempo.Api.Auth;

/// <summary>
/// Lê a identidade que o provedor externo depositou no esquema <see cref="AuthSchemes.External"/>
/// ao voltar do fluxo authorization code.
///
/// É uma porta externa — o provedor OAuth é exatamente o tipo de dependência que o teste de
/// contrato pode dublar (§6.4), o que permite exercitar vínculo, criação e recusa sem falar
/// com o Google.
/// </summary>
public interface IExternalIdentityReader
{
    Task<ExternalIdentity?> ReadAsync(HttpContext context);
}
