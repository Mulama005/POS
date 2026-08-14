using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Interfaces;
using Pos.Infrastructure.Auth;
using Pos.Infrastructure.Email;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Storage;

namespace Pos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Database ---
        services.AddDbContext<PosDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection")));

        // --- MFA / auth helpers ---
        services.AddDataProtection();
        services.AddMemoryCache();
        services.AddScoped<IMfaService, MfaService>();
        services.AddSingleton<IMfaChallengeStore, MemoryCacheMfaChallengeStore>();

        // --- File storage ---
        services.AddSupabaseStorage(configuration);

        // --- Email ---
        services.AddScoped<IEmailSender, ConsoleEmailSender>();

        return services;
    }
}