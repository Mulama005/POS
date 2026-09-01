using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Interfaces;
using Pos.Infrastructure.Auth;
using Pos.Infrastructure.Email;
using Pos.Infrastructure.Messaging;
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
        services.AddSingleton<IMfaService, MfaService>();
        services.AddSingleton<IMfaChallengeStore, MemoryCacheMfaChallengeStore>();
        services.AddSingleton<IDiscountApprovalStore, MemoryCacheDiscountApprovalStore>();

        // --- File storage ---
        // AddHttpClient<TClient,TImplementation> already fully registers IStorageService with
        // a properly wired HttpClient — a second AddScoped<IStorageService, ...> here used to
        // overwrite that registration ("last registration wins"), silently handing
        // SupabaseStorageService a bare, unconfigured default HttpClient instead. That's what
        // caused uploads to fail with "invalid request URI... BaseAddress must be set" even
        // when Supabase:Url was configured correctly.
        services.AddHttpClient<IStorageService, SupabaseStorageService>();

        // --- Email ---
        services.AddScoped<IEmailSender, ConsoleEmailSender>();

        // --- WhatsApp Cloud API ---
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.AddHttpClient<IWhatsAppService, WhatsAppCloudApiService>();

        return services;
    }
}