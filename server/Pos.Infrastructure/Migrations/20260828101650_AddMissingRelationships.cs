using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryAdjustments_Units_UnitId",
                table: "InventoryAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_Units_UnitId",
                table: "SaleItems");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropColumn(
                name: "ExpectedCashAtOpen",
                table: "Registers");

            migrationBuilder.DropColumn(
                name: "IsTillOpen",
                table: "Registers");

            migrationBuilder.DropColumn(
                name: "WarrantyMonthsOverride",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "UnitId",
                table: "SaleItems",
                newName: "StockUnitId");

            migrationBuilder.RenameIndex(
                name: "IX_SaleItems_UnitId",
                table: "SaleItems",
                newName: "IX_SaleItems_StockUnitId");

            migrationBuilder.RenameColumn(
                name: "UnitId",
                table: "InventoryAdjustments",
                newName: "StockUnitId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryAdjustments_UnitId",
                table: "InventoryAdjustments",
                newName: "IX_InventoryAdjustments_StockUnitId");

            migrationBuilder.AlterColumn<string>(
                name: "Barcode",
                table: "Products",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryAdjustments_StockUnits_StockUnitId",
                table: "InventoryAdjustments",
                column: "StockUnitId",
                principalTable: "StockUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_StockUnits_StockUnitId",
                table: "SaleItems",
                column: "StockUnitId",
                principalTable: "StockUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryAdjustments_StockUnits_StockUnitId",
                table: "InventoryAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_StockUnits_StockUnitId",
                table: "SaleItems");

            migrationBuilder.RenameColumn(
                name: "StockUnitId",
                table: "SaleItems",
                newName: "UnitId");

            migrationBuilder.RenameIndex(
                name: "IX_SaleItems_StockUnitId",
                table: "SaleItems",
                newName: "IX_SaleItems_UnitId");

            migrationBuilder.RenameColumn(
                name: "StockUnitId",
                table: "InventoryAdjustments",
                newName: "UnitId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryAdjustments_StockUnitId",
                table: "InventoryAdjustments",
                newName: "IX_InventoryAdjustments_UnitId");

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedCashAtOpen",
                table: "Registers",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTillOpen",
                table: "Registers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Barcode",
                table: "Products",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyMonthsOverride",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SoldOnSaleItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Imei = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WarrantyExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Units_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Units_SaleItems_SoldOnSaleItemId",
                        column: x => x.SoldOnSaleItemId,
                        principalTable: "SaleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Units_Imei",
                table: "Units",
                column: "Imei",
                unique: true,
                filter: "\"Imei\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Units_ProductId",
                table: "Units",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_SerialNumber",
                table: "Units",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_SoldOnSaleItemId",
                table: "Units",
                column: "SoldOnSaleItemId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryAdjustments_Units_UnitId",
                table: "InventoryAdjustments",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_Units_UnitId",
                table: "SaleItems",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
