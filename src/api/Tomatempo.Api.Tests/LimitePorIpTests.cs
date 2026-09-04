using System.Net;
using System.Net.Http.Json;
using Tomatempo.Api.Tests.Harness;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Critério da §5: "Endpoint anônimo de autenticação responde 429 ao passar de 60 requisições
/// por minuto vindas do mesmo IP."
///
/// Vive em classe própria porque cada classe de teste tem sua própria instância da API — e,
/// com ela, seu próprio contador. Esgotar a janela aqui não afeta os outros testes.
/// </summary>
public sealed class LimitePorIpTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    /// <summary>
    /// Confirmação de e-mail é o alvo certo do limite por IP: não tem e-mail no corpo para
    /// particionar por conta, e é a porta que hoje aceitaria tentativa de token sem fim.
    /// </summary>
    [Fact]
    public async Task Confirmar_DeveResponder429_AoPassarDeSessentaTentativasNoMinuto()
    {
        // Arrange
        var client = NovoCliente();
        var respostas = new List<HttpResponseMessage>();

        // Act
        for (var tentativa = 1; tentativa <= 61; tentativa++)
        {
            respostas.Add(await client.PostAsync(
                "/api/auth/confirm-email", JsonContent.Create(new { token = $"token-inventado-{tentativa}" })));
        }

        // Assert
        respostas.Take(60).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.BadRequest);
        respostas[60].StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
