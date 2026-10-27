using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSkuSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE SEQUENCE "ProductSkuSequence" AS bigint START WITH 1;
                SELECT setval(
                    '"ProductSkuSequence"',
                    GREATEST(COALESCE(MAX(SUBSTRING("Sku" FROM 5)::bigint), 1), 1),
                    COUNT(*) > 0)
                FROM "Products"
                WHERE "Sku" ~ '^EDT-[0-9]+$';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP SEQUENCE IF EXISTS "ProductSkuSequence";""");
        }
    }
}
