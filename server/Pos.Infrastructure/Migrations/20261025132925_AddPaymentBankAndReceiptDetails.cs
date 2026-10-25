using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentBankAndReceiptDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsManualMpesa",
                table: "Payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "CreditTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                table: "CreditTransactions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IsManualMpesa",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "CreditTransactions");

            migrationBuilder.DropColumn(
                name: "ExternalReference",
                table: "CreditTransactions");
        }
    }
}
