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
            var connectionString = "Host=aws-0-eu-west-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.wwvjazbgvhoptqpwkaye;Password=YrD8bdzHAZPJuusx;SslMode=Require;TrustServerCertificate=true";

            optionsBuilder.UseNpgsql(connectionString);

            return new PosDbContext(optionsBuilder.Options);
        }
    }
}