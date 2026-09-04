using System.Collections.Concurrent;
using Tomatempo.Domain.Ports;

namespace Tomatempo.Api.Tests.Harness;

public enum SentEmailKind
{
    EmailConfirmation,
    PasswordReset,
    DuplicateRegistrationNotice,
}

public sealed record SentEmail(SentEmailKind Kind, string Email, string? Token, string Locale);

/// <summary>
/// Dublê da porta de e-mail (§6.4). Guarda o que sairia na caixa de entrada para que o teste
/// possa usar o token — é como o cenário "usuário clica no link" é montado sem SMTP.
/// </summary>
public sealed class RecordingEmailSender : IAccountEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyList<SentEmail> Sent => _sent.ToArray();

    public Task SendEmailConfirmationAsync(string email, string token, string locale, CancellationToken cancellationToken)
    {
        _sent.Enqueue(new SentEmail(SentEmailKind.EmailConfirmation, email, token, locale));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string token, string locale, CancellationToken cancellationToken)
    {
        _sent.Enqueue(new SentEmail(SentEmailKind.PasswordReset, email, token, locale));
        return Task.CompletedTask;
    }

    public Task SendDuplicateRegistrationNoticeAsync(string email, string locale, CancellationToken cancellationToken)
    {
        _sent.Enqueue(new SentEmail(SentEmailKind.DuplicateRegistrationNotice, email, Token: null, locale));
        return Task.CompletedTask;
    }

    public IReadOnlyList<SentEmail> For(string email) =>
        Sent.Where(e => string.Equals(e.Email, email, StringComparison.OrdinalIgnoreCase)).ToArray();

    /// <summary>Último token enviado para o e-mail no tipo pedido.</summary>
    public string TokenFor(string email, SentEmailKind kind) =>
        For(email).Where(e => e.Kind == kind).Select(e => e.Token).LastOrDefault()
        ?? throw new InvalidOperationException($"Nenhum e-mail do tipo {kind} foi enviado para {email}.");
}
