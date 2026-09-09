using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Tomatempo.Api.Services;
using Tomatempo.Domain.Time;
using Tomatempo.Infrastructure.Entities;

namespace Tomatempo.Api.Endpoints;

/// <summary>Estatísticas pessoais (§4). O dia é sempre o dia local do dono da sessão (§3.6).</summary>
public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/stats").RequireAuthorization();

        grupo.MapGet("/today", HojeAsync);
    }

    private static async Task<IResult> HojeAsync(
        ClaimsPrincipal principal,
        UserManager<TomatempoUser> users,
        SessionWorkflow sessions,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var user = (await users.GetUserAsync(principal))!;
        var hoje = LocalDay.Of(clock.GetUtcNow(), SessionWorkflow.FusoDe(user));
        var dia = await sessions.DiaAsync(user, hoje, cancellationToken);

        return Results.Ok(new TodayView(
            hoje,
            dia.CompletedPomodoros,
            dia.FocusMinutes,
            // Minutos que valem para o ranking: só os validados no servidor, de conta com
            // e-mail confirmado e sem opt-out (§3.4, §3.7).
            dia.OnlineFocusMinutes,
            dia.Goal,
            dia.GoalMet));
    }
}
