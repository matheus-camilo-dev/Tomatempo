namespace Tomatempo.Domain.Ports;

/// <summary>
/// Porta externa de e-mail (§6.4 — é o tipo de dependência que pode ser dublada no teste).
/// A aplicação decide *o que* mandar e para quem; a infraestrutura decide como montar e
/// entregar. O token viaja como valor, nunca embutido em texto pronto pela aplicação.
/// </summary>
public interface IAccountEmailSender
{
    Task SendEmailConfirmationAsync(string email, string token, string locale, CancellationToken cancellationToken);

    Task SendPasswordResetAsync(string email, string token, string locale, CancellationToken cancellationToken);

    /// <summary>
    /// Avisa o dono do e-mail de que alguém tentou se cadastrar com ele. Existe para que a
    /// resposta do cadastro seja igual nos dois casos, sem criar a segunda conta (§3.10).
    /// </summary>
    Task SendDuplicateRegistrationNoticeAsync(string email, string locale, CancellationToken cancellationToken);
}
