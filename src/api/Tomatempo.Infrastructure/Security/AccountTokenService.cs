using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Tomatempo.Domain.Accounts;
using Tomatempo.Infrastructure.Entities;

namespace Tomatempo.Infrastructure.Security;

/// <summary>
/// Emite e consome os tokens de e-mail (§3.10). O texto que vai no link existe só na memória
/// desta chamada e no e-mail do dono; o banco guarda o hash. Consumir é uma operação só,
/// atômica, o que é o que faz "uso único" valer mesmo com dois cliques ao mesmo tempo.
/// </summary>
public sealed class AccountTokenService(TomatempoDbContext db, TimeProvider clock)
{
    public async Task<string> IssueAsync(Guid userId, AccountTokenKind kind, CancellationToken cancellationToken)
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        db.AccountTokens.Add(new AccountTokenRow
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Kind = kind,
            TokenHash = Hash(token),
            IssuedAt = clock.GetUtcNow(),
        });

        await db.SaveChangesAsync(cancellationToken);
        return token;
    }

    /// <summary>Devolve o dono do token, ou <c>null</c> se ele não existe, já foi usado ou venceu.</summary>
    public async Task<Guid?> ConsumeAsync(string? token, AccountTokenKind kind, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = Hash(token);
        var now = clock.GetUtcNow();

        var row = await db.AccountTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash && t.Kind == kind, cancellationToken);

        if (row is null || row.ConsumedAt is not null || !AccountTokens.IsValid(kind, row.IssuedAt, now))
        {
            return null;
        }

        row.ConsumedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return row.UserId;
    }

    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
