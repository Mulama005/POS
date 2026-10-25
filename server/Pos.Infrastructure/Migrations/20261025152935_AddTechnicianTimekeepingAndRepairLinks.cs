using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicianTimekeepingAndRepairLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RepairJobId",
                table: "CreditTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "CreditTransactions" AS ct
                SET "RepairJobId" = r."Id"
                FROM "RepairJobs" AS r
                WHERE ct."RepairJobId" IS NULL
                  AND ct."Notes" LIKE 'Repair ' || r."TicketNumber" || ' payment%';
                """);

            migrationBuilder.CreateTable(
                name: "TechnicianTimeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClockInAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClockInRecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClockOutAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClockOutRecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianTimeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianTimeEntries_DomainUsers_ClockInRecordedByUserId",
                        column: x => x.ClockInRecordedByUserId,
                        principalTable: "DomainUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicianTimeEntries_DomainUsers_ClockOutRecordedByUserId",
                        column: x => x.ClockOutRecordedByUserId,
                        principalTable: "DomainUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicianTimeEntries_DomainUsers_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "DomainUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_RepairJobId",
                table: "CreditTransactions",
                column: "RepairJobId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTimeEntries_ClockInAt",
                table: "TechnicianTimeEntries",
                column: "ClockInAt");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTimeEntries_ClockInRecordedByUserId",
                table: "TechnicianTimeEntries",
                column: "ClockInRecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTimeEntries_ClockOutRecordedByUserId",
                table: "TechnicianTimeEntries",
                column: "ClockOutRecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTimeEntries_TechnicianId",
                table: "TechnicianTimeEntries",
                column: "TechnicianId",
                unique: true,
                filter: "\"ClockOutAt\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CreditTransactions_RepairJobs_RepairJobId",
                table: "CreditTransactions",
                column: "RepairJobId",
                principalTable: "RepairJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditTransactions_RepairJobs_RepairJobId",
                table: "CreditTransactions");

            migrationBuilder.DropTable(
                name: "TechnicianTimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_CreditTransactions_RepairJobId",
                table: "CreditTransactions");

            migrationBuilder.DropColumn(
                name: "RepairJobId",
                table: "CreditTransactions");
        }
    }
}
