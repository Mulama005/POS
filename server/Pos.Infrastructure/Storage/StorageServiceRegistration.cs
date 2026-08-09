using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Storage;

public static class StorageServiceRegistration
{
    /// <summary>
    /// Call this from your existing DependencyInjection.cs, inside AddInfrastructure(...):
    ///     services.AddSupabaseStorage(configuration);
    /// </summary>
    public static IServiceCollection AddSupabaseStorage(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind the configuration section into the options instance. Using a lambda avoids
        // ambiguity with other Configure overloads when the IConfiguration-based extension
        // is not picked up by the compiler.
        services.Configure<SupabaseStorageOptions>(opts =>
        {
            var section = configuration.GetSection(SupabaseStorageOptions.SectionName);
            opts.Url = section["Url"] ?? string.Empty;
            opts.ServiceRoleKey = section["ServiceRoleKey"] ?? string.Empty;
            opts.Bucket = section["Bucket"] ?? opts.Bucket;
        });
        services.AddScoped<IStorageService, SupabaseStorageService>();
        return services;
    }
}
