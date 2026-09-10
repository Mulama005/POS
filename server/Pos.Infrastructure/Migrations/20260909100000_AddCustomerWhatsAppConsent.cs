using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Persistence;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(PosDbContext))]
    [Migration("20260909100000_AddCustomerWhatsAppConsent")]
    public partial class AddCustomerWhatsAppConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppOptIn",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WhatsAppOptedInAt",
                table: "Customers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "WhatsAppOptIn", table: "Customers");
            migrationBuilder.DropColumn(name: "WhatsAppOptedInAt", table: "Customers");
        }
    }
}
