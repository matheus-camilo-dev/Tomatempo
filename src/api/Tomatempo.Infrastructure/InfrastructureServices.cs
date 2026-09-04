using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tomatempo.Domain.Ports;
using Tomatempo.Infrastructure.Email;
using Tomatempo.Infrastructure.Security;

namespace Tomatempo.Infrastructure;

public static class InfrastructureServices
{
    public static IServiceCollection AddTomatempoInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Db")
            ?? throw new InvalidOperationException("ConnectionStrings:Db não está configurada.");

        services.AddDbContext<TomatempoDbContext>(options => options.UseNpgsql(connectionString));

        // O relógio é injetado em tudo (§6.4); `DateTime.UtcNow` não aparece fora daqui.
        services.TryAddTimeProvider();

        services.AddSingleton<IPasswordPolicy, CommonPasswordPolicy>();
        services.AddScoped<AccountTokenService>();
        services.AddScoped<IAccountEmailSender, SmtpAccountEmailSender>();

        return services;
    }

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (services.All(s => s.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
