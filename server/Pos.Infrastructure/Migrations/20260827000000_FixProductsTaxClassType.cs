using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Persistence;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <summary>
    /// Corrects Products.TaxClass, which is still stored as text due to a prior
    /// migration whose conversion SQL was malformed and silently no-opped.
    /// Converts named/legacy values to the integer enum the app expects.
    /// </summary>
    [DbContext(typeof(PosDbContext))]
    [Migration("20260827000000_FixProductsTaxClassType")]
    public partial class FixProductsTaxClassType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                """
                ALTER TABLE "Products"
                ALTER COLUMN "TaxClass" TYPE text
                USING CASE "TaxClass"
                    WHEN 0 THEN 'Standard'
                    WHEN 1 THEN 'ZeroRated'
                    WHEN 2 THEN 'Exempt'
                    ELSE 'Standard'
                END;
                """);
        }
    }
}