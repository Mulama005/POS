using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Interfaces;
using Pos.Infrastructure.Auth;
using Pos.Infrastructure.Email;
using Pos.Infrastructure.Messaging;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Storage;
using Pos.Infrastructure.Services;

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
        services.AddSingleton<IMfaService, MfaService>();
        services.AddSingleton<IMfaChallengeStore, MemoryCacheMfaChallengeStore>();
        services.AddSingleton<IDiscountApprovalStore, MemoryCacheDiscountApprovalStore>();

        // --- File storage ---
        services.AddHttpClient<IStorageService, SupabaseStorageService>();
        services.AddScoped<IStorageService, SupabaseStorageService>();

        // --- Email ---
        services.AddScoped<IEmailSender, ConsoleEmailSender>();

        // --- WhatsApp Cloud API ---
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.AddHttpClient<IWhatsAppService, WhatsAppCloudApiService>();
        
        // --- Auditing ---
        services.AddScoped<IAuditService, AuditService>();

        return services;
    }
}
