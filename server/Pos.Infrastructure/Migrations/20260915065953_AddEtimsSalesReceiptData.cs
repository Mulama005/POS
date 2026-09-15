using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEtimsSalesReceiptData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_EtimsItemCode",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_EtimsRegisteredAt",
                table: "Products");

            migrationBuilder.DropSequence(
                name: "EtimsItemCodeSequence");

            migrationBuilder.AddColumn<string>(
                name: "EtimsInternalData",
                table: "Sales",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EtimsMrcNo",
                table: "Sales",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EtimsReceiptNumber",
                table: "Sales",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EtimsReceiptPublishedDate",
                table: "Sales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EtimsReceiptSignature",
                table: "Sales",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EtimsResultCode",
                table: "Sales",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EtimsSdcId",
                table: "Sales",
                type: "character varying(18)",
                maxLength: 18,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EtimsTotalReceiptNumber",
                table: "Sales",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EtimsQuantityUnitCode",
                table: "Products",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EtimsPackagingUnitCode",
                table: "Products",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EtimsOriginCountryCode",
                table: "Products",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EtimsItemTypeCode",
                table: "Products",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EtimsItemCode",
                table: "Products",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EtimsInternalData",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "EtimsMrcNo",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "EtimsReceiptNumber",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "EtimsReceiptPublishedDate",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "EtimsReceiptSignature",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "EtimsResultCode",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "EtimsSdcId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "EtimsTotalReceiptNumber",
                table: "Sales");

            migrationBuilder.CreateSequence(
                name: "EtimsItemCodeSequence");

            migrationBuilder.AlterColumn<string>(
                name: "EtimsQuantityUnitCode",
                table: "Products",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EtimsPackagingUnitCode",
                table: "Products",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EtimsOriginCountryCode",
                table: "Products",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EtimsItemTypeCode",
                table: "Products",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EtimsItemCode",
                table: "Products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_EtimsItemCode",
                table: "Products",
                column: "EtimsItemCode",
                unique: true,
                filter: "\"EtimsItemCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_EtimsRegisteredAt",
                table: "Products",
                column: "EtimsRegisteredAt");
        }
    }
}
