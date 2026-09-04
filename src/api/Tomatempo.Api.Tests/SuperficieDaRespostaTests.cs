using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tomatempo.Api.Tests.Harness;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Critérios da §5: "Nenhuma resposta traz o cabeçalho `Server`", "Endpoint que muda estado
/// recusa requisição com `Content-Type` de formulário" e "O fluxo do Google está configurado
/// com PKCE".
///
/// O teste de PKCE olha a opção efetiva do pipeline, não a intenção do código — é a forma que
/// a §7.3.4 pede para pegar default inseguro herdado de exemplo.
/// </summary>
public sealed class SuperficieDaRespostaTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    [Fact]
    public void Kestrel_NaoDeveAnunciarOServidor()
    {
        // Arrange — a asserção é sobre a opção efetiva, e não sobre a resposta: o servidor de
        // teste não emite `Server` nem sem a configuração, então um teste de cabeçalho passaria
        // vazio e não protegeria nada.

        // Act
        var kestrel = Api.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        // Assert
        kestrel.AddServerHeader.Should().BeFalse();
    }

    [Fact]
    public async Task EndpointQueMudaEstado_DeveRecusarContentTypeDeFormulario()
    {
        // Arrange — formulário de outro site só consegue mandar estes tipos; recusá-los fecha
        // o caminho de CSRF que sobra além do SameSite=Lax (§7.2/A05).
        var conta = await CadastrarAsync();
        var comoFormulario = new StringContent("", Encoding.UTF8, "application/x-www-form-urlencoded");

        // Act
        var response = await conta.Client.PostAsync("/api/auth/logout", comoFormulario);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task EndpointQueMudaEstado_DeveContinuarAceitandoJson()
    {
        // Arrange — a recusa acima não pode ter apanhado o caminho normal junto.
        var conta = await CadastrarAsync();

        // Act
        var response = await conta.Client.PostAsync("/api/auth/logout", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public void Google_DeveEstarConfiguradoComPkce()
    {
        // Act
        var options = Api.Services
            .GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get(GoogleDefaults.AuthenticationScheme);

        // Assert
        options.UsePkce.Should().BeTrue();
    }
}
