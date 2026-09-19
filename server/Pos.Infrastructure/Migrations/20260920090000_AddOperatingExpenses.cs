using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Persistence;

#nullable disable

namespace Pos.Infrastructure.Migrations;

[DbContext(typeof(PosDbContext))]
[Migration("20260920090000_AddOperatingExpenses")]
public partial class AddOperatingExpenses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OperatingExpenses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                MonthlyAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_OperatingExpenses", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_OperatingExpenses_Name_IsActive",
            table: "OperatingExpenses",
            columns: new[] { "Name", "IsActive" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "OperatingExpenses");
}
