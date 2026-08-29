using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestoreTaxClassEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TaxClass",
                table: "Products",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TaxClass",
                table: "Products",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
