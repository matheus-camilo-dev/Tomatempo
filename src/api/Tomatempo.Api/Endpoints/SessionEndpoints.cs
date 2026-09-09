using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Tomatempo.Api.Services;
using Tomatempo.Domain;
using Tomatempo.Infrastructure.Entities;

namespace Tomatempo.Api.Endpoints;

/// <summary>
/// Sessões de foco validadas no servidor (§3.2, §3.3).
///
/// O contrato de entrada é quase vazio de propósito: o cliente diz que quer começar, bate
/// heartbeat e diz que terminou. Início, fim, duração e minutos são todos carimbo do servidor
/// — não existe campo para o cliente mandar nenhum deles (§7.3.3).
/// </summary>
public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/sessions").RequireAuthorization();

        grupo.MapPost("/start", IniciarAsync);
        grupo.MapPost("/{id:guid}/beat", BaterAsync).WithMetadata(new HeartbeatEndpoint());
        grupo.MapPost("/{id:guid}/complete", ConcluirAsync);
        grupo.MapPost("/{id:guid}/abandon", AbandonarAsync);
        grupo.MapGet("/active", AtivaAsync);
    }

    private static async Task<IResult> IniciarAsync(
        StartSessionRequest? request,
        ClaimsPrincipal principal,
        UserManager<TomatempoUser> users,
        SessionWorkflow sessions,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var user = (await users.GetUserAsync(principal))!;

        // Conta que não terminou de nascer não pontua nada (§3.10).
        if (user.OnboardingCompletedAt is null)
        {
            return Problems.Conflict(Problems.OnboardingRequired, "Conta ainda não foi completada.");
        }

        var sessao = await sessions.StartAsync(user, cancellationToken);
        var agora = clock.GetUtcNow();

        return Results.Ok(new StartedSessionView(
            sessao.Id,
            sessao.StartedAtUtc,
            sessao.StartedAtUtc + RuleConstants.FocusDuration,
            agora));
    }

    private static async Task<IResult> BaterAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<TomatempoUser> users,
        SessionWorkflow sessions,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var user = (await users.GetUserAsync(principal))!;
        var (sessao, falha) = await sessions.BeatAsync(user, id, cancellationToken);

        if (sessao is null) return Recusa(falha!.Value);

        var agora = clock.GetUtcNow();
        return Results.Ok(new HeartbeatView(true, agora, RestamSegundos(sessao, agora)));
    }

    private static async Task<IResult> ConcluirAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<TomatempoUser> users,
        SessionWorkflow sessions,
        CancellationToken cancellationToken)
    {
        var user = (await users.GetUserAsync(principal))!;
        var (sessao, falha) = await sessions.SettleAsync(user, id, cancellationToken);

        if (sessao is null) return Recusa(falha!.Value);

        var dia = await sessions.DiaAsync(user, sessao.LocalDate, cancellationToken);

        return Results.Ok(new CompletedSessionView(
            sessao.CountedMinutes, dia.CompletedPomodoros, dia.Goal, dia.GoalMet));
    }

    private static async Task<IResult> AbandonarAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<TomatempoUser> users,
        SessionWorkflow sessions,
        CancellationToken cancellationToken)
    {
        var user = (await users.GetUserAsync(principal))!;
        var (sessao, falha) = await sessions.SettleAsync(user, id, cancellationToken);

        if (sessao is null) return Recusa(falha!.Value);

        return Results.Ok(new AbandonedSessionView(
            sessao.CountedMinutes, sessao.CountedMinutes == 0));
    }

    private static async Task<IResult> AtivaAsync(
        ClaimsPrincipal principal,
        UserManager<TomatempoUser> users,
        SessionWorkflow sessions,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var user = (await users.GetUserAsync(principal))!;
        var sessao = await sessions.AtivaAsync(user.Id, cancellationToken);

        if (sessao is null) return Results.NoContent();

        var agora = clock.GetUtcNow();
        return Results.Ok(new ActiveSessionView(
            sessao.Id,
            sessao.StartedAtUtc,
            sessao.StartedAtUtc + RuleConstants.FocusDuration,
            agora,
            RestamSegundos(sessao, agora)));
    }

    /// <summary>
    /// Sessão de outra pessoa responde igual a sessão inexistente. Um 404 que se distingue de
    /// um 403 é um oráculo: diria quais ids existem (§7.2/A01).
    /// </summary>
    private static IResult Recusa(SessionFault falha) => falha switch
    {
        SessionFault.NotFound => Problems.NotFoundResult(),
        SessionFault.Superseded => Problems.Conflict(
            Problems.SessionSuperseded, "Esta sessão foi assumida em outro dispositivo."),
        SessionFault.Expired => Problems.Conflict(
            Problems.SessionExpired, "Esta sessão expirou por falta de sinal."),
        _ => Problems.Conflict(Problems.SessionClosed, "Esta sessão já foi encerrada."),
    };

    private static int RestamSegundos(FocusSessionRow sessao, DateTimeOffset agora)
    {
        var restante = sessao.StartedAtUtc + RuleConstants.FocusDuration - agora;

        return restante <= TimeSpan.Zero ? 0 : (int)restante.TotalSeconds;
    }
}
