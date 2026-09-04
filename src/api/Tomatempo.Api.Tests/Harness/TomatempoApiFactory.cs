using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Tomatempo.Api.Auth;
using Tomatempo.Domain.Ports;

namespace Tomatempo.Api.Tests.Harness;

/// <summary>
/// A API de verdade, contra o Postgres de verdade. Só três coisas são trocadas, e todas são
/// portas externas (§6.4): o relógio, o envio de e-mail e o provedor OAuth. Repositório,
/// DbContext e regra de domínio rodam como em produção.
/// </summary>
public sealed class TomatempoApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Meio-dia UTC = 9h em São Paulo: nem virada de dia, nem borda de fuso por acidente.</summary>
    public static readonly DateTimeOffset InicioDoTeste = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString;

    public TomatempoApiFactory(string connectionString) => _connectionString = connectionString;

    public FakeTimeProvider Clock { get; } = new(InicioDoTeste);

    public RecordingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Db", _connectionString);
        builder.UseSetting("Auth:Google:ClientId", "fake-client-id.apps.googleusercontent.com");
        builder.UseSetting("Auth:Google:ClientSecret", "changeme");
        builder.UseSetting("App:PublicUrl", "https://localhost");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            services.RemoveAll<IAccountEmailSender>();
            services.AddSingleton<IAccountEmailSender>(Emails);

            services.RemoveAll<IExternalIdentityReader>();
            services.AddSingleton<IExternalIdentityReader, HeaderExternalIdentityReader>();
        });
    }

    /// <summary>
    /// Cliente com cookies e sem seguir redirecionamento. O endereço é https porque o cookie
    /// de sessão sai com `Secure` (§7.2/A02) e um cliente em http simplesmente o descartaria,
    /// escondendo a flag em vez de prová-la.
    /// </summary>
    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true,
    });
}
