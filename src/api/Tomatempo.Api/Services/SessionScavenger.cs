using Microsoft.EntityFrameworkCore;
using Tomatempo.Domain;
using Tomatempo.Domain.Sessions;
using Tomatempo.Infrastructure;

namespace Tomatempo.Api.Services;

/// <summary>
/// Varredor de sessões órfãs (§3.2). Sem heartbeat há mais de dois minutos, a sessão é
/// encerrada como <c>Expired</c> e o parcial é apurado pelas mesmas regras — a aba que morreu
/// não deixa sessão aberta para sempre, e o tempo em que ninguém estava lá não vira minuto.
///
/// Serviço, e não `BackgroundService`: quem acorda de tempos em tempos é
/// <see cref="SessionScavengerService"/>. Separar deixa o teste de contrato chamar a varredura
/// no instante que ele escolher, em vez de esperar o relógio.
/// </summary>
public sealed class SessionScavenger(TomatempoDbContext db, SessionWorkflow sessions, TimeProvider clock)
{
    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var agora = clock.GetUtcNow();
        var corte = agora - RuleConstants.ScavengerIdleLimit;

        var orfas = await db.FocusSessions
            .Where(f => f.Status == SessionStatus.Active
                && (f.LastHeartbeatAtUtc == null || f.LastHeartbeatAtUtc < corte))
            .ToListAsync(cancellationToken);

        foreach (var sessao in orfas)
        {
            var user = await db.Users.FirstAsync(u => u.Id == sessao.UserId, cancellationToken);
            await sessions.EncerrarAsync(user, sessao, SessionStatus.Expired, agora, cancellationToken);
        }

        return orfas.Count;
    }
}
