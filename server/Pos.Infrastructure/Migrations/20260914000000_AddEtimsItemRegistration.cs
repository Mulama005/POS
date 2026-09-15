using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations;

public partial class AddEtimsItemRegistration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateSequence<long>(
            name: "EtimsItemCodeSequence",
            startValue: 1L,
            incrementBy: 1);

        migrationBuilder.AddColumn<string>(
            name: "EtimsItemCode",
            table: "Products",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EtimsItemTypeCode",
            table: "Products",
            type: "character varying(5)",
            maxLength: 5,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EtimsOriginCountryCode",
            table: "Products",
            type: "character varying(5)",
            maxLength: 5,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EtimsPackagingUnitCode",
            table: "Products",
            type: "character varying(5)",
            maxLength: 5,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EtimsQuantityUnitCode",
            table: "Products",
            type: "character varying(5)",
            maxLength: 5,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "EtimsRegisteredAt",
            table: "Products",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "EtimsRegisteredByUserId",
            table: "Products",
            type: "uuid",
            nullable: true);

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

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Products_EtimsItemCode",
            table: "Products");

        migrationBuilder.DropIndex(
            name: "IX_Products_EtimsRegisteredAt",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "EtimsItemCode",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "EtimsItemTypeCode",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "EtimsOriginCountryCode",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "EtimsPackagingUnitCode",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "EtimsQuantityUnitCode",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "EtimsRegisteredAt",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "EtimsRegisteredByUserId",
            table: "Products");

        migrationBuilder.DropSequence(
            name: "EtimsItemCodeSequence");
    }
}