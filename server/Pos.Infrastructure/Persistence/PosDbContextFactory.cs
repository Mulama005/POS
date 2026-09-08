using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Persistence
{
    /// <summary>
    /// Used only by `dotnet ef` design-time tools (migrations add/update) to construct a
    /// PosDbContext without running the full Program.cs host.
    ///
    /// Reads the connection string the same way the running app does — Pos.API's
    /// user-secrets first (identified explicitly by its UserSecretsId, since this class
    /// lives in Pos.Infrastructure, not Pos.API, so the generic AddUserSecrets&lt;T&gt;
    /// convention doesn't apply here) — falling back to the
    /// ConnectionStrings__DefaultConnection environment variable if secrets aren't found.
    /// This deliberately never reads a hardcoded value from source, so nothing here can
    /// leak a real credential into git again.
    /// </summary>
    public class PosDbContextFactory : IDesignTimeDbContextFactory<PosDbContext>
    {
        // Matches <UserSecretsId> in server/Pos.API/Pos.API.csproj.
        private const string ApiUserSecretsId = "80e91065-b1e7-4400-a470-bc246b79a521";

        public PosDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets(ApiUserSecretsId, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string not found. Set it via " +
                    "'dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...\" " +
                    "--project Pos.API' (preferred, matches the running app), or the " +
                    "ConnectionStrings__DefaultConnection environment variable.");

            var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new PosDbContext(optionsBuilder.Options);
        }
    }
}