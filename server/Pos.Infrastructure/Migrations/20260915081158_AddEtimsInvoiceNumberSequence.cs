using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEtimsInvoiceNumberSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE SEQUENCE "EtimsInvoiceNumberSequence"
                START WITH 1
                INCREMENT BY 1
                MINVALUE 1
                NO MAXVALUE
                CACHE 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP SEQUENCE IF EXISTS "EtimsInvoiceNumberSequence";
                """);
        }
    }
}