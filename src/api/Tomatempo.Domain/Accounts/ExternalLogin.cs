using Tomatempo.Domain;

namespace Tomatempo.Domain.Accounts;

/// <summary>Identidade devolvida por um provedor externo (hoje só o Google).</summary>
public sealed record ExternalIdentity(string Provider, string Subject, string Email, bool EmailVerified);

/// <summary>O que fazer com uma identidade externa que acabou de chegar.</summary>
public enum ExternalLoginDecision
{
    /// <summary>O provedor não afirma que o e-mail é dele: entrar seria tomar a conta alheia.</summary>
    RejectUnverifiedEmail,

    /// <summary>Esse provedor já está vinculado a uma conta: é só entrar.</summary>
    SignInLinked,

    /// <summary>O e-mail já tem conta de senha: vincula o provedor a ela, sem criar a segunda.</summary>
    LinkToExistingAccount,

    /// <summary>E-mail novo: cria a conta, que nasce incompleta até o onboarding.</summary>
    CreateAccount,
}

/// <summary>
/// Regra de vínculo do login social (§3.10, log de 03/09/2026). Duas contas para a mesma
/// pessoa duplicariam streak e minutos de ranking — por isso o e-mail conhecido nunca gera
/// conta nova, e o e-mail não verificado nunca entra.
/// </summary>
public static class ExternalLogin
{
    public static ExternalLoginDecision Decide(
        ExternalIdentity identity,
        bool providerAlreadyLinked,
        bool emailAlreadyRegistered)
    {
        if (!identity.EmailVerified)
        {
            return ExternalLoginDecision.RejectUnverifiedEmail;
        }

        if (providerAlreadyLinked)
        {
            return ExternalLoginDecision.SignInLinked;
        }

        return emailAlreadyRegistered
            ? ExternalLoginDecision.LinkToExistingAccount
            : ExternalLoginDecision.CreateAccount;
    }
}
