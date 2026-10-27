using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyRepairJobIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RepairJobs_Customers_CustomerId",
                table: "RepairJobs");

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "RepairPayments",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                table: "RepairPayments",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "RepairPayments",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                table: "RepairJobs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_RepairJobs_Customers_CustomerId",
                table: "RepairJobs",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.Sql("""
                INSERT INTO "RepairPayments"
                    ("Id", "RepairJobId", "Amount", "Status", "CheckoutRequestId", "MpesaReceiptNumber",
                     "PhoneNumber", "InitiatedByUserId", "CreatedAt", "ProcessedAt", "PaymentMethod", "BankName", "ExternalReference")
                SELECT gen_random_uuid(), ct."RepairJobId", ct."Amount", 1, NULL,
                       CASE WHEN ct."PaymentMethod" ILIKE 'M-Pesa' THEN ct."ExternalReference" ELSE NULL END,
                       NULL, ct."RecordedByUserId", ct."Timestamp", ct."Timestamp",
                       ct."PaymentMethod", ct."BankName", ct."ExternalReference"
                FROM "CreditTransactions" AS ct
                WHERE ct."RepairJobId" IS NOT NULL
                  AND ct."Type" = 1
                  AND NOT EXISTS (
                      SELECT 1 FROM "RepairPayments" AS attempt
                      WHERE attempt."RepairJobId" = ct."RepairJobId"
                        AND attempt."Amount" = ct."Amount"
                        AND attempt."Status" = 1
                        AND ct."ExternalReference" IS NOT NULL
                        AND (attempt."MpesaReceiptNumber" = ct."ExternalReference"
                             OR attempt."CheckoutRequestId" = ct."ExternalReference")
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RepairJobs_Customers_CustomerId",
                table: "RepairJobs");

            migrationBuilder.Sql("""
                DO $$
                DECLARE fallback_customer_id uuid;
                BEGIN
                    SELECT "Id" INTO fallback_customer_id
                    FROM "Customers"
                    WHERE "Email" = 'anonymous-repairs@local.invalid'
                    LIMIT 1;

                    IF fallback_customer_id IS NULL THEN
                        fallback_customer_id := gen_random_uuid();
                        INSERT INTO "Customers"
                            ("Id", "FullName", "Phone", "Email", "Type", "PricingTier", "CreditLimit", "CurrentCreditBalance", "CreatedAt", "UpdatedAt")
                        VALUES
                            (fallback_customer_id, 'Anonymous repair', NULL, 'anonymous-repairs@local.invalid', 0, NULL, 0, 0, NOW(), NULL);
                    END IF;

                    UPDATE "RepairJobs" SET "CustomerId" = fallback_customer_id WHERE "CustomerId" IS NULL;
                END $$;
                """);

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "RepairPayments");

            migrationBuilder.DropColumn(
                name: "ExternalReference",
                table: "RepairPayments");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "RepairPayments");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                table: "RepairJobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RepairJobs_Customers_CustomerId",
                table: "RepairJobs",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
