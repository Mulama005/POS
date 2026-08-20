using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTillSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedCashAtOpen",
                table: "Registers");

            migrationBuilder.DropColumn(
                name: "IsTillOpen",
                table: "Registers");

            migrationBuilder.AddColumn<Guid>(
                name: "TillSessionId",
                table: "Sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TillSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpeningFloat = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpectedCashAtClose = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CountedCashAtClose = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    VarianceAtClose = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TillSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TillSessions_DomainUsers_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "DomainUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TillSessions_DomainUsers_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalTable: "DomainUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TillSessions_Registers_RegisterId",
                        column: x => x.RegisterId,
                        principalTable: "Registers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_TillSessionId",
                table: "Sales",
                column: "TillSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TillSessions_ClosedByUserId",
                table: "TillSessions",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TillSessions_OpenedByUserId",
                table: "TillSessions",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TillSessions_RegisterId",
                table: "TillSessions",
                column: "RegisterId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_TillSessions_TillSessionId",
                table: "Sales",
                column: "TillSessionId",
                principalTable: "TillSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_TillSessions_TillSessionId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "TillSessions");

            migrationBuilder.DropIndex(
                name: "IX_Sales_TillSessionId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TillSessionId",
                table: "Sales");

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
        }
    }
}
