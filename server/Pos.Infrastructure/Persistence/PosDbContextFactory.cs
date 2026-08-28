using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Persistence
{
    public class PosDbContextFactory : IDesignTimeDbContextFactory<PosDbContext>
    {
        public PosDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();

            // ⚠️ Replace with your actual Supabase connection string
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                                   ?? throw new InvalidOperationException(
                                       "Connection string not found. Set the environment variable 'ConnectionStrings__DefaultConnection'.");

            optionsBuilder.UseNpgsql(connectionString);

            return new PosDbContext(optionsBuilder.Options);
        }
    }
}