using System.Text.Json;

namespace Tomatempo.Api;

/// <summary>
/// Toda resposta de erro sai daqui: `application/problem+json` com um <c>code</c> estável.
/// O front traduz pelo code, nunca pela mensagem (§4) — e a mensagem, por isso mesmo, nunca
/// carrega detalhe interno: nada de stack, SQL, nome de coluna ou existência de conta (§7.4).
/// </summary>
public static class Problems
{
    public const string Unauthenticated = "unauthenticated";
    public const string Forbidden = "forbidden";
    public const string InvalidRequest = "invalid_request";
    public const string InvalidCredentials = "invalid_credentials";
    public const string InvalidEmail = "invalid_email";
    public const string InvalidNickname = "invalid_nickname";
    public const string NicknameTaken = "nickname_taken";
    public const string NicknameLocked = "nickname_locked";
    public const string InvalidGoal = "invalid_goal";
    public const string InvalidTimeZone = "invalid_time_zone";
    public const string InvalidLocale = "invalid_locale";
    public const string InvalidColorMode = "invalid_color_mode";
    public const string WeakPassword = "weak_password";
    public const string InvalidToken = "invalid_token";
    public const string OnboardingRequired = "onboarding_required";
    public const string OnboardingAlreadyCompleted = "onboarding_already_completed";
    public const string NotFound = "not_found";

    /// <summary>Outra aba assumiu o timer; esta para de pontuar (§3.3).</summary>
    public const string SessionSuperseded = "session_superseded";

    /// <summary>O varredor encerrou a sessão por falta de heartbeat (§3.2).</summary>
    public const string SessionExpired = "session_expired";

    /// <summary>A sessão já foi encerrada por conclusão ou abandono.</summary>
    public const string SessionClosed = "session_closed";

    public const string TooManyRequests = "too_many_requests";
    public const string UnsupportedMediaType = "unsupported_media_type";
    public const string InternalError = "internal_error";

    public static IResult Of(int status, string code, string title) =>
        Results.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    public static IResult BadRequest(string code, string title) => Of(StatusCodes.Status400BadRequest, code, title);

    public static IResult Conflict(string code, string title) => Of(StatusCodes.Status409Conflict, code, title);

    /// <summary>
    /// Recurso que não é do dono da sessão responde igual a recurso inexistente: 404 que
    /// revela a existência de um é um oráculo de enumeração (§7.2/A01).
    /// </summary>
    public static IResult NotFoundResult() =>
        Of(StatusCodes.Status404NotFound, NotFound, "Não encontrado.");

    /// <summary>
    /// Versão para os pontos que não devolvem <see cref="IResult"/> — eventos do cookie e o
    /// tratador de exceção, que escrevem direto na resposta.
    /// </summary>
    public static async Task WriteAsync(HttpResponse response, int status, string code, string title)
    {
        response.StatusCode = status;
        response.ContentType = "application/problem+json";

        var problema = new Dictionary<string, object?>
        {
            ["type"] = "about:blank",
            ["title"] = title,
            ["status"] = status,
            ["code"] = code,
        };

        await response.WriteAsync(JsonSerializer.Serialize(problema));
    }
}
