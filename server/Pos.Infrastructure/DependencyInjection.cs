using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Interfaces;
using Pos.Infrastructure.Auth;
using Pos.Infrastructure.Email;
using Pos.Infrastructure.Etims;
using Pos.Infrastructure.Messaging;
using Pos.Infrastructure.Payments;
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

        // --- Daraja (M-Pesa STK Push), Step 27 ---
        // (Note: this section was accidentally duplicated verbatim in a previous
        // session's edit — harmless since AddHttpClient/Configure are idempotent here,
        // but removed the second copy while touching this file for Step 26.)
        services.Configure<DarajaOptions>(configuration.GetSection(DarajaOptions.SectionName));
        services.AddHttpClient<IDarajaService, DarajaService>();

        // --- eTIMS (KRA VSCU), Step 26 ---
        // Talks to a locally-running VSCU JAR, not a remote KRA server — see
        // EtimsOptions for why BaseUrl is a local address.
        services.Configure<EtimsOptions>(configuration.GetSection(EtimsOptions.SectionName));
        services.AddHttpClient<IEtimsService, EtimsService>();

        return services;
    }
}