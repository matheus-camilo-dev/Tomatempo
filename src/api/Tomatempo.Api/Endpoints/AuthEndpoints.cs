using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tomatempo.Api.Auth;
using Tomatempo.Api.Services;
using Tomatempo.Domain.Accounts;
using Tomatempo.Domain.Ports;
using Tomatempo.Infrastructure;
using Tomatempo.Infrastructure.Entities;
using Tomatempo.Infrastructure.Security;

namespace Tomatempo.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/auth");

        grupo.MapPost("/register", RegistrarAsync);
        grupo.MapPost("/login", EntrarAsync);
        grupo.MapPost("/logout", SairAsync).RequireAuthorization();
        grupo.MapPost("/confirm-email", ConfirmarEmailAsync);
        grupo.MapPost("/resend-confirmation", ReenviarConfirmacaoAsync);
        grupo.MapPost("/forgot-password", EsquecerSenhaAsync);
        grupo.MapPost("/reset-password", ResetarSenhaAsync);
        grupo.MapPost("/onboarding", ConcluirOnboardingAsync).RequireAuthorization();
        grupo.MapGet("/google", DesafiarGoogle);
        grupo.MapGet("/google/callback", GoogleCallbackAsync);
    }

    /// <summary>
    /// Cadastro por e-mail e senha. A resposta é a mesma para e-mail novo e para e-mail que já
    /// tem conta — no segundo caso nenhuma conta é criada e quem recebe aviso é o dono do
    /// endereço (§3.10). A sessão sai junto no primeiro caso, e é o resíduo assumido no log de
    /// 03/09/2026: só ela distingue os dois para quem observa a resposta crua.
    /// </summary>
    private static async Task<IResult> RegistrarAsync(
        RegisterRequest request,
        UserManager<TomatempoUser> users,
        SignInManager<TomatempoUser> signIn,
        AccountWorkflow workflow,
        AccountTokenService tokens,
        IAccountEmailSender emails,
        IPasswordPolicy passwords,
        LoginThrottle throttle,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // Mesmo limite do login (§3.10): é ele que contém o resíduo de enumeração do cadastro,
        // já que a presença do cookie distingue e-mail novo de e-mail que já tem conta.
        if (!throttle.Allow(request.Email))
        {
            return Problems.Of(StatusCodes.Status429TooManyRequests, Problems.TooManyRequests, "Tentativas demais.");
        }

        var invalido = ValidarPerfil(request.Nickname, request.Goal, request.TimeZone, request.Locale, out var apelido);
        if (invalido is not null)
        {
            return invalido;
        }

        if (!EmailValido(request.Email))
        {
            return Problems.BadRequest(Problems.InvalidEmail, "E-mail inválido.");
        }

        if (!passwords.IsAcceptable(request.Password))
        {
            return Problems.BadRequest(Problems.WeakPassword, "Senha muito curta ou muito comum.");
        }

        if (await workflow.ApelidoEmUsoAsync(apelido!.Value, exceto: null, cancellationToken))
        {
            return Problems.Conflict(Problems.NicknameTaken, "Apelido já está em uso.");
        }

        var jaCadastrado = await users.FindByEmailAsync(request.Email!);
        if (jaCadastrado is not null)
        {
            await emails.SendDuplicateRegistrationNoticeAsync(request.Email!, jaCadastrado.Locale, cancellationToken);
            return Results.Accepted();
        }

        var agora = clock.GetUtcNow();
        var user = new TomatempoUser
        {
            Id = Guid.CreateVersion7(),
            UserName = request.Email,
            Email = request.Email,
            Nickname = apelido.Value,
            TimeZone = request.TimeZone!,
            Locale = request.Locale!,
            ColorMode = "system",
            CreatedAt = agora,
            OnboardingCompletedAt = agora,
        };

        var criado = await users.CreateAsync(user, request.Password!);
        if (!criado.Succeeded)
        {
            return Problems.BadRequest(Problems.InvalidRequest, "Não foi possível criar a conta com esses dados.");
        }

        await workflow.SemearContaAsync(user, request.Goal, cancellationToken);

        var token = await tokens.IssueAsync(user.Id, AccountTokenKind.EmailConfirmation, cancellationToken);
        await emails.SendEmailConfirmationAsync(user.Email!, token, user.Locale, cancellationToken);

        await signIn.SignInAsync(user, isPersistent: true);
        return Results.Accepted();
    }

    private static async Task<IResult> EntrarAsync(
        CredentialsRequest request,
        UserManager<TomatempoUser> users,
        SignInManager<TomatempoUser> signIn,
        LoginThrottle throttle,
        CancellationToken cancellationToken)
    {
        if (!throttle.Allow(request.Email))
        {
            return Problems.Of(StatusCodes.Status429TooManyRequests, Problems.TooManyRequests, "Tentativas demais.");
        }

        var user = request.Email is null ? null : await users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Mesma resposta de senha errada: a lista de quem tem conta não sai daqui (§7.2/A07).
            return Problems.Of(StatusCodes.Status401Unauthorized, Problems.InvalidCredentials, "Credenciais inválidas.");
        }

        var resultado = await signIn.PasswordSignInAsync(user, request.Password ?? string.Empty, isPersistent: true, lockoutOnFailure: true);
        return resultado.Succeeded
            ? Results.NoContent()
            : Problems.Of(StatusCodes.Status401Unauthorized, Problems.InvalidCredentials, "Credenciais inválidas.");
    }

    private static async Task<IResult> SairAsync(SignInManager<TomatempoUser> signIn)
    {
        await signIn.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> ConfirmarEmailAsync(
        TokenRequest request,
        UserManager<TomatempoUser> users,
        AccountTokenService tokens,
        CancellationToken cancellationToken)
    {
        var userId = await tokens.ConsumeAsync(request.Token, AccountTokenKind.EmailConfirmation, cancellationToken);
        if (userId is null)
        {
            return Problems.BadRequest(Problems.InvalidToken, "Token inválido ou vencido.");
        }

        var user = (await users.FindByIdAsync(userId.Value.ToString()))!;
        user.EmailConfirmed = true;
        await users.UpdateAsync(user);

        return Results.NoContent();
    }

    private static async Task<IResult> ReenviarConfirmacaoAsync(
        EmailRequest request,
        UserManager<TomatempoUser> users,
        AccountTokenService tokens,
        IAccountEmailSender emails,
        LoginThrottle throttle,
        CancellationToken cancellationToken)
    {
        if (!throttle.Allow(request.Email))
        {
            return Problems.Of(StatusCodes.Status429TooManyRequests, Problems.TooManyRequests, "Tentativas demais.");
        }

        var user = request.Email is null ? null : await users.FindByEmailAsync(request.Email);
        if (user is not null && !user.EmailConfirmed)
        {
            var token = await tokens.IssueAsync(user.Id, AccountTokenKind.EmailConfirmation, cancellationToken);
            await emails.SendEmailConfirmationAsync(user.Email!, token, user.Locale, cancellationToken);
        }

        // 202 sempre: com ou sem conta naquele endereço, a resposta é a mesma (§3.10).
        return Results.Accepted();
    }

    private static async Task<IResult> EsquecerSenhaAsync(
        EmailRequest request,
        UserManager<TomatempoUser> users,
        AccountTokenService tokens,
        IAccountEmailSender emails,
        LoginThrottle throttle,
        CancellationToken cancellationToken)
    {
        if (!throttle.Allow(request.Email))
        {
            return Problems.Of(StatusCodes.Status429TooManyRequests, Problems.TooManyRequests, "Tentativas demais.");
        }

        var user = request.Email is null ? null : await users.FindByEmailAsync(request.Email);
        if (user is not null)
        {
            var token = await tokens.IssueAsync(user.Id, AccountTokenKind.PasswordReset, cancellationToken);
            await emails.SendPasswordResetAsync(user.Email!, token, user.Locale, cancellationToken);
        }

        return Results.Accepted();
    }

    private static async Task<IResult> ResetarSenhaAsync(
        ResetPasswordRequest request,
        UserManager<TomatempoUser> users,
        AccountTokenService tokens,
        IPasswordPolicy passwords,
        CancellationToken cancellationToken)
    {
        // A senha é conferida antes de gastar o token: recusar por senha fraca não pode custar
        // ao usuário o link que ele acabou de receber.
        if (!passwords.IsAcceptable(request.Password))
        {
            return Problems.BadRequest(Problems.WeakPassword, "Senha muito curta ou muito comum.");
        }

        var userId = await tokens.ConsumeAsync(request.Token, AccountTokenKind.PasswordReset, cancellationToken);
        if (userId is null)
        {
            return Problems.BadRequest(Problems.InvalidToken, "Token inválido ou vencido.");
        }

        var user = (await users.FindByIdAsync(userId.Value.ToString()))!;
        user.PasswordHash = users.PasswordHasher.HashPassword(user, request.Password!);

        // Trocar o carimbo de segurança derruba as sessões abertas — inclusive a de quem
        // roubou a senha, que é o motivo de existir o reset (§3.10).
        await users.UpdateSecurityStampAsync(user);

        return Results.NoContent();
    }

    private static async Task<IResult> ConcluirOnboardingAsync(
        OnboardingRequest request,
        ClaimsPrincipal principal,
        UserManager<TomatempoUser> users,
        AccountWorkflow workflow,
        TomatempoDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var user = (await users.GetUserAsync(principal))!;
        if (user.OnboardingCompletedAt is not null)
        {
            return Problems.Conflict(Problems.OnboardingAlreadyCompleted, "Conta já está completa.");
        }

        var invalido = ValidarPerfil(request.Nickname, request.Goal, request.TimeZone, request.Locale, out var apelido);
        if (invalido is not null)
        {
            return invalido;
        }

        if (await workflow.ApelidoEmUsoAsync(apelido!.Value, user.Id, cancellationToken))
        {
            return Problems.Conflict(Problems.NicknameTaken, "Apelido já está em uso.");
        }

        user.Nickname = apelido.Value;
        user.TimeZone = request.TimeZone!;
        user.Locale = request.Locale!;
        user.OnboardingCompletedAt = clock.GetUtcNow();
        await users.UpdateAsync(user);

        workflow.RegistrarMetaInicial(user, request.Goal);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static IResult DesafiarGoogle(IConfiguration config) =>
        Results.Challenge(
            new AuthenticationProperties { RedirectUri = "/api/auth/google/callback" },
            ["Google"]);

    /// <summary>
    /// Volta do Google: vincula à conta existente, entra na já vinculada ou cria uma incompleta.
    /// Sem <c>email_verified</c> do provedor, nada disso acontece — seria deixar qualquer um
    /// dizer que é dono do endereço alheio (§3.10).
    /// </summary>
    private static async Task<IResult> GoogleCallbackAsync(
        HttpContext http,
        IExternalIdentityReader reader,
        UserManager<TomatempoUser> users,
        SignInManager<TomatempoUser> signIn,
        AccountWorkflow workflow,
        IConfiguration config,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var publicUrl = config["App:PublicUrl"] ?? string.Empty;

        var identity = await reader.ReadAsync(http);
        if (identity is null)
        {
            return Results.Redirect($"{publicUrl}/entrar?erro=external_login_failed");
        }

        var vinculado = await users.FindByLoginAsync(identity.Provider, identity.Subject);
        var porEmail = await users.FindByEmailAsync(identity.Email);

        var decisao = ExternalLogin.Decide(identity, vinculado is not null, porEmail is not null);
        if (decisao == ExternalLoginDecision.RejectUnverifiedEmail)
        {
            return Results.Redirect($"{publicUrl}/entrar?erro=external_email_not_verified");
        }

        var user = decisao switch
        {
            ExternalLoginDecision.SignInLinked => vinculado!,
            ExternalLoginDecision.LinkToExistingAccount => porEmail!,
            _ => await CriarContaExternaAsync(identity, users, workflow, clock, cancellationToken),
        };

        if (decisao != ExternalLoginDecision.SignInLinked)
        {
            await users.AddLoginAsync(user, new UserLoginInfo(identity.Provider, identity.Subject, identity.Provider));
        }

        await http.SignOutAsync(AuthSchemes.External);
        await signIn.SignInAsync(user, isPersistent: true);

        return Results.Redirect(user.OnboardingCompletedAt is null ? $"{publicUrl}/onboarding" : $"{publicUrl}/");
    }

    private static async Task<TomatempoUser> CriarContaExternaAsync(
        ExternalIdentity identity,
        UserManager<TomatempoUser> users,
        AccountWorkflow workflow,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var user = new TomatempoUser
        {
            Id = Guid.CreateVersion7(),
            UserName = identity.Email,
            Email = identity.Email,
            // O provedor garantiu o endereço; exigir confirmação de novo seria pedir prova do
            // que já foi provado (§3.10).
            EmailConfirmed = true,
            Nickname = null,
            TimeZone = "UTC",
            Locale = "pt-BR",
            ColorMode = "system",
            CreatedAt = clock.GetUtcNow(),
            OnboardingCompletedAt = null,
        };

        await users.CreateAsync(user);
        await workflow.SemearContaAsync(user, meta: null, cancellationToken);
        return user;
    }

    /// <summary>Validação comum ao cadastro e ao onboarding, na mesma ordem nos dois.</summary>
    private static IResult? ValidarPerfil(
        string? apelidoBruto,
        int meta,
        string? fuso,
        string? idioma,
        out Nickname? apelido)
    {
        if (Nickname.TryCreate(apelidoBruto, out apelido) != NicknameError.None)
        {
            return Problems.BadRequest(Problems.InvalidNickname, "Apelido inválido.");
        }

        if (DailyGoal.ValidateSignup(meta) != GoalError.None)
        {
            return Problems.BadRequest(Problems.InvalidGoal, "Meta inválida.");
        }

        if (!AccountWorkflow.FusoValido(fuso))
        {
            return Problems.BadRequest(Problems.InvalidTimeZone, "Fuso horário inválido.");
        }

        if (!AccountWorkflow.IdiomaValido(idioma))
        {
            return Problems.BadRequest(Problems.InvalidLocale, "Idioma não suportado.");
        }

        return null;
    }

    private static bool EmailValido(string? email) =>
        !string.IsNullOrWhiteSpace(email) && new EmailAddressAttribute().IsValid(email);
}
