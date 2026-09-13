using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEtimsClassificationToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "KraTaxTypeCode",
                table: "Products",
                newName: "EtimsTaxTypeCode");

            migrationBuilder.RenameColumn(
                name: "KraItemClassificationCode",
                table: "Products",
                newName: "EtimsItemClassificationCode");

            migrationBuilder.RenameColumn(
                name: "KraClassifiedByUserId",
                table: "Products",
                newName: "EtimsClassifiedByUserId");

            migrationBuilder.RenameColumn(
                name: "KraClassifiedAt",
                table: "Products",
                newName: "EtimsClassifiedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Products_KraItemClassificationCode",
                table: "Products",
                newName: "IX_Products_EtimsItemClassificationCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EtimsTaxTypeCode",
                table: "Products",
                newName: "KraTaxTypeCode");

            migrationBuilder.RenameColumn(
                name: "EtimsItemClassificationCode",
                table: "Products",
                newName: "KraItemClassificationCode");

            migrationBuilder.RenameColumn(
                name: "EtimsClassifiedByUserId",
                table: "Products",
                newName: "KraClassifiedByUserId");

            migrationBuilder.RenameColumn(
                name: "EtimsClassifiedAt",
                table: "Products",
                newName: "KraClassifiedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Products_EtimsItemClassificationCode",
                table: "Products",
                newName: "IX_Products_KraItemClassificationCode");
        }
    }
}
