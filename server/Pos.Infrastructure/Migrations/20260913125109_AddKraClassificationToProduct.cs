using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKraClassificationToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "KraClassifiedAt",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KraClassifiedByUserId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KraItemClassificationCode",
                table: "Products",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KraTaxTypeCode",
                table: "Products",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ItemClsNm",
                table: "EtimsItemClasses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_Products_KraItemClassificationCode",
                table: "Products",
                column: "KraItemClassificationCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_KraItemClassificationCode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "KraClassifiedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "KraClassifiedByUserId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "KraItemClassificationCode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "KraTaxTypeCode",
                table: "Products");

            migrationBuilder.AlterColumn<string>(
                name: "ItemClsNm",
                table: "EtimsItemClasses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
