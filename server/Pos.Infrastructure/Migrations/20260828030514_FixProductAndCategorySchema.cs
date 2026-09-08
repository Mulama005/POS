using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    partial class FixProductAndCategorySchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Force Sku and Barcode to be varchar(64) to fix the ILIKE text search crash
            migrationBuilder.Sql("ALTER TABLE \"Products\" ALTER COLUMN \"Sku\" TYPE varchar(64) USING \"Sku\"::varchar;");
            migrationBuilder.Sql("ALTER TABLE \"Products\" ALTER COLUMN \"Barcode\" TYPE varchar(64) USING \"Barcode\"::varchar;");

            // 2. Force integer columns to actually be integer (fixes the InvalidCastException)
            migrationBuilder.Sql("ALTER TABLE \"Products\" ALTER COLUMN \"StockQuantity\" TYPE integer USING \"StockQuantity\"::integer;");
            migrationBuilder.Sql("ALTER TABLE \"Products\" ALTER COLUMN \"ReorderThreshold\" TYPE integer USING \"ReorderThreshold\"::integer;");
            migrationBuilder.Sql("ALTER TABLE \"Products\" ALTER COLUMN \"TaxClass\" TYPE integer USING \"TaxClass\"::integer;");

            // 3. Add ALL missing columns to Categories
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Categories' AND column_name='ParentCategoryId') THEN
                        ALTER TABLE ""Categories"" ADD COLUMN ""ParentCategoryId"" uuid NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Categories' AND column_name='RequiresSerialTracking') THEN
                        ALTER TABLE ""Categories"" ADD COLUMN ""RequiresSerialTracking"" boolean NOT NULL DEFAULT false;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Categories' AND column_name='DefaultWarrantyMonths') THEN
                        ALTER TABLE ""Categories"" ADD COLUMN ""DefaultWarrantyMonths"" integer NOT NULL DEFAULT 0;
                    END IF;
                END $$;");

            // 4. Add missing WarrantyMonthsOverride to Products
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Products' AND column_name='WarrantyMonthsOverride') THEN
                        ALTER TABLE ""Products"" ADD COLUMN ""WarrantyMonthsOverride"" integer NULL;
                    END IF;
                END $$;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Products\" ALTER COLUMN \"Sku\" TYPE text;");
            migrationBuilder.Sql("ALTER TABLE \"Products\" ALTER COLUMN \"Barcode\" TYPE text;");
            migrationBuilder.Sql("ALTER TABLE \"Products\" ALTER COLUMN \"StockQuantity\" TYPE text;");
            migrationBuilder.Sql("ALTER TABLE \"Products\" ALTER COLUMN \"ReorderThreshold\" TYPE text;");
            migrationBuilder.Sql("ALTER TABLE \"Products\" ALTER COLUMN \"TaxClass\" TYPE text;");
            migrationBuilder.Sql("ALTER TABLE \"Categories\" DROP COLUMN IF EXISTS \"ParentCategoryId\";");
            migrationBuilder.Sql("ALTER TABLE \"Categories\" DROP COLUMN IF EXISTS \"RequiresSerialTracking\";");
            migrationBuilder.Sql("ALTER TABLE \"Categories\" DROP COLUMN IF EXISTS \"DefaultWarrantyMonths\";");
            migrationBuilder.Sql("ALTER TABLE \"Products\" DROP COLUMN IF EXISTS \"WarrantyMonthsOverride\";");
        }
    }
}