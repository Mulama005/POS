using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Persistence;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <summary>
    /// Repairs a schema-drifted database where the initial migration was recorded but
    /// the Products.StockQuantity column was not present. The conditional PostgreSQL
    /// statements also keep new and already-correct databases safe to upgrade.
    /// </summary>
    [DbContext(typeof(PosDbContext))]
    [Migration("20260826211500_RestoreProductStockQuantity")]
    public partial class RestoreProductStockQuantity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"Products\" ADD COLUMN IF NOT EXISTS \"StockQuantity\" integer NOT NULL DEFAULT 0;");

            // A legacy database stored this enum as text, while the application and
            // original migration use its integer representation. Convert both named
            // values and old numeric strings without losing existing product records.
            migrationBuilder.Sql(
    """
    DO $$
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'Products'
              AND column_name = 'TaxClass'
              AND data_type = 'text'
        ) THEN
            ALTER TABLE "Products"
            ALTER COLUMN "TaxClass" TYPE integer
            USING CASE lower("TaxClass")
                WHEN 'standard' THEN 0
                WHEN 'zerorated' THEN 1
                WHEN 'zero-rated' THEN 1
                WHEN 'exempt' THEN 2
                ELSE CASE
                    WHEN "TaxClass" ~ '^[0-9]+$' THEN "TaxClass"::integer
                    ELSE 0
                END
            END;
        END IF;
    END $$;
    """);
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"Products\" DROP COLUMN IF EXISTS \"StockQuantity\";");
        }
    }
}
