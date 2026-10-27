using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierSourcedPurchasesAndCostSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseCost",
                table: "StockUnits",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPriceAtSale",
                table: "SaleItems",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierSourcedPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierSourcedPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierSourcedPurchases_DomainUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "DomainUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierSourcedPurchases_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSourcedPurchases_ProductId",
                table: "SupplierSourcedPurchases",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSourcedPurchases_PurchasedAt",
                table: "SupplierSourcedPurchases",
                column: "PurchasedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSourcedPurchases_RecordedByUserId",
                table: "SupplierSourcedPurchases",
                column: "RecordedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierSourcedPurchases");

            migrationBuilder.DropColumn(
                name: "PurchaseCost",
                table: "StockUnits");

            migrationBuilder.DropColumn(
                name: "CostPriceAtSale",
                table: "SaleItems");
        }
    }
}
