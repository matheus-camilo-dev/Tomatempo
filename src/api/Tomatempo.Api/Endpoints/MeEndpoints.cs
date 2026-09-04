using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Tomatempo.Api.Services;
using Tomatempo.Domain.Accounts;
using Tomatempo.Infrastructure.Entities;

namespace Tomatempo.Api.Endpoints;

public static class MeEndpoints
{
    public static void MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/me").RequireAuthorization();

        grupo.MapGet("/", VerPerfilAsync);
        grupo.MapPatch("/", AtualizarPreferenciasAsync);
        grupo.MapPut("/goal", AlterarMetaAsync);
        grupo.MapGet("/freezes", VerCongelamentosAsync);
    }

    private static async Task<IResult> VerPerfilAsync(
        ClaimsPrincipal principal,
        UserManager<TomatempoUser> users,
        AccountWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var user = (await users.GetUserAsync(principal))!;
        return Results.Ok(await workflow.MontarPerfilAsync(user, cancellationToken));
    }

    /// <summary>
    /// Preferências do dono da sessão. O usuário alvo é sempre o da sessão: apelido, id ou
    /// qualquer outro identificador que venha no corpo ou na query é simplesmente ignorado,
    /// porque não existe campo para ele no contrato de entrada (§7.2/A01, §7.3.1).
    /// </summary>
    private static async Task<IResult> AtualizarPreferenciasAsync(
        PreferencesRequest request,
        ClaimsPrincipal principal,
        UserManager<TomatempoUser> users,
        AccountWorkflow workflow,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var user = (await users.GetUserAsync(principal))!;

        if (request.Nickname is not null)
        {
            var recusa = await TrocarApelidoAsync(request.Nickname, user, workflow, clock, cancellationToken);
            if (recusa is not null)
            {
                return recusa;
            }
        }

        if (request.TimeZone is not null)
        {
            if (!AccountWorkflow.FusoValido(request.TimeZone))
            {
                return Problems.BadRequest(Problems.InvalidTimeZone, "Fuso horário inválido.");
            }

            user.TimeZone = request.TimeZone;
        }

        if (request.Locale is not null)
        {
            if (!AccountWorkflow.IdiomaValido(request.Locale))
            {
                return Problems.BadRequest(Problems.InvalidLocale, "Idioma não suportado.");
            }

            user.Locale = request.Locale;
        }

        if (request.ColorMode is not null)
        {
            if (!AccountWorkflow.ModoDeCorValido(request.ColorMode))
            {
                return Problems.BadRequest(Problems.InvalidColorMode, "Modo de cor inválido.");
            }

            user.ColorMode = request.ColorMode;
        }

        if (request.RankingOptOut is not null)
        {
            user.RankingOptOut = request.RankingOptOut.Value;
        }

        await users.UpdateAsync(user);
        return Results.Ok(await workflow.MontarPerfilAsync(user, cancellationToken));
    }

    private static async Task<IResult?> TrocarApelidoAsync(
        string apelidoBruto,
        TomatempoUser user,
        AccountWorkflow workflow,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (Nickname.TryCreate(apelidoBruto, out var apelido) != NicknameError.None)
        {
            return Problems.BadRequest(Problems.InvalidNickname, "Apelido inválido.");
        }

        if (apelido!.Value == user.Nickname)
        {
            return null;
        }

        if (!NicknameChangePolicy.CanChange(user.NicknameChangedAt, clock.GetUtcNow()))
        {
            return Problems.Conflict(Problems.NicknameLocked, "Apelido só pode ser trocado a cada 30 dias.");
        }

        if (await workflow.ApelidoEmUsoAsync(apelido.Value, user.Id, cancellationToken))
        {
            return Problems.Conflict(Problems.NicknameTaken, "Apelido já está em uso.");
        }

        user.Nickname = apelido.Value;
        user.NicknameChangedAt = clock.GetUtcNow();
        return null;
    }

    private static async Task<IResult> AlterarMetaAsync(
        GoalRequest request,
        ClaimsPrincipal principal,
        UserManager<TomatempoUser> users,
        AccountWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var user = (await users.GetUserAsync(principal))!;

        if (user.OnboardingCompletedAt is null)
        {
            return Problems.Conflict(Problems.OnboardingRequired, "Conta ainda não foi completada.");
        }

        if (DailyGoal.ValidateChange(request.Pomodoros) != GoalError.None)
        {
            return Problems.BadRequest(Problems.InvalidGoal, "Meta inválida.");
        }

        return Results.Ok(await workflow.AlterarMetaAsync(user, request.Pomodoros, cancellationToken));
    }

    private static async Task<IResult> VerCongelamentosAsync(
        ClaimsPrincipal principal,
        UserManager<TomatempoUser> users,
        AccountWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var user = (await users.GetUserAsync(principal))!;
        return Results.Ok(await workflow.MontarCongelamentosAsync(user, cancellationToken));
    }
}
