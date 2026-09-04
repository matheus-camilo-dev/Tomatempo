using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tomatempo.Domain.Ports;

namespace Tomatempo.Infrastructure.Email;

/// <summary>
/// Envio por SMTP — em desenvolvimento o destino é o mailpit do compose (§10). Nenhum e-mail
/// completo entra em log (§7.2/A09): o que se registra é o tipo de mensagem, não para quem.
/// </summary>
public sealed class SmtpAccountEmailSender(IConfiguration config, ILogger<SmtpAccountEmailSender> logger)
    : IAccountEmailSender
{
    private const string Remetente = "nao-responda@tomatempo.app";

    private string PublicUrl => config["App:PublicUrl"] ?? "http://localhost:5173";

    public Task SendEmailConfirmationAsync(string email, string token, string locale, CancellationToken cancellationToken)
    {
        var link = $"{PublicUrl}/confirmar-email?token={Uri.EscapeDataString(token)}";
        var (assunto, corpo) = locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
            ? ("Confirme seu e-mail no Tomatempo",
               $"Confirme seu e-mail para participar do ranking. O link vale 24 horas:\n\n{link}")
            : ("Confirm your Tomatempo e-mail",
               $"Confirm your e-mail to join the ranking. This link is valid for 24 hours:\n\n{link}");

        return EnviarAsync(email, assunto, corpo, cancellationToken);
    }

    public Task SendPasswordResetAsync(string email, string token, string locale, CancellationToken cancellationToken)
    {
        var link = $"{PublicUrl}/redefinir-senha?token={Uri.EscapeDataString(token)}";
        var (assunto, corpo) = locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
            ? ("Redefinir sua senha do Tomatempo",
               $"Se foi você quem pediu, use o link abaixo. Ele vale 1 hora e serve uma vez só:\n\n{link}")
            : ("Reset your Tomatempo password",
               $"If this was you, use the link below. It is valid for 1 hour and works only once:\n\n{link}");

        return EnviarAsync(email, assunto, corpo, cancellationToken);
    }

    public Task SendDuplicateRegistrationNoticeAsync(string email, string locale, CancellationToken cancellationToken)
    {
        var (assunto, corpo) = locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
            ? ("Alguém tentou criar uma conta com seu e-mail",
               $"Este endereço já tem conta no Tomatempo, então nenhuma conta nova foi criada. " +
               $"Se foi você, entre normalmente ou recupere a senha em {PublicUrl}/entrar.")
            : ("Someone tried to sign up with your e-mail",
               $"This address already has a Tomatempo account, so no new account was created. " +
               $"If this was you, sign in or reset your password at {PublicUrl}/entrar.");

        return EnviarAsync(email, assunto, corpo, cancellationToken);
    }

    private async Task EnviarAsync(string destino, string assunto, string corpo, CancellationToken cancellationToken)
    {
        var host = config["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            // Ambiente sem SMTP configurado é erro de operação, não caso de negócio: falha alto.
            throw new InvalidOperationException("Smtp:Host não está configurado.");
        }

        using var client = new SmtpClient(host, int.Parse(config["Smtp:Port"] ?? "1025"));
        using var mensagem = new MailMessage(Remetente, destino, assunto, corpo);

        await client.SendMailAsync(mensagem, cancellationToken);
        logger.LogInformation("E-mail de conta enviado ({Assunto}).", assunto);
    }
}
