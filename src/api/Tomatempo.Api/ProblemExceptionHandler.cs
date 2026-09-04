using Microsoft.AspNetCore.Diagnostics;

namespace Tomatempo.Api;

/// <summary>
/// Última barreira antes da resposta: nenhuma exceção vira corpo. Corpo malformado é erro do
/// cliente e sai como 400 com code estável; o resto sai como 500 genérico, com o detalhe indo
/// para o log e não para quem chamou (§7.2/A05, §7.4).
/// </summary>
public sealed class ProblemExceptionHandler(ILogger<ProblemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is BadHttpRequestException)
        {
            await Problems.WriteAsync(
                context.Response, StatusCodes.Status400BadRequest, Problems.InvalidRequest, "Requisição inválida.");
            return true;
        }

        logger.LogError(exception, "Falha não tratada ao processar {Metodo} {Rota}.",
            context.Request.Method, context.Request.Path);

        await Problems.WriteAsync(
            context.Response, StatusCodes.Status500InternalServerError, Problems.InternalError, "Erro interno.");
        return true;
    }
}
