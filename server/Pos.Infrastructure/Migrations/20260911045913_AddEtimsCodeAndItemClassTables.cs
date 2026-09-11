using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEtimsCodeAndItemClassTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EtimsCodeClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CdCls = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CdClsNm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CdClsDesc = table.Column<string>(type: "text", nullable: true),
                    UserDfnNm1 = table.Column<string>(type: "text", nullable: true),
                    UserDfnNm2 = table.Column<string>(type: "text", nullable: true),
                    UserDfnNm3 = table.Column<string>(type: "text", nullable: true),
                    UseYn = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtimsCodeClasses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EtimsItemClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemClsCd = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ItemClsNm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemClsLvl = table.Column<int>(type: "integer", nullable: false),
                    TaxTyCd = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    MjrTgYn = table.Column<bool>(type: "boolean", nullable: true),
                    UseYn = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtimsItemClasses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EtimsSyncStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastSuccessfulSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAttemptSucceeded = table.Column<bool>(type: "boolean", nullable: false),
                    LastAttemptMessage = table.Column<string>(type: "text", nullable: true),
                    LastAttemptRecordCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtimsSyncStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EtimsCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EtimsCodeClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cd = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    CdNm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CdDesc = table.Column<string>(type: "text", nullable: true),
                    SrtOrd = table.Column<int>(type: "integer", nullable: false),
                    UserDfnCd1 = table.Column<string>(type: "text", nullable: true),
                    UserDfnCd2 = table.Column<string>(type: "text", nullable: true),
                    UserDfnCd3 = table.Column<string>(type: "text", nullable: true),
                    UseYn = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtimsCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EtimsCodes_EtimsCodeClasses_EtimsCodeClassId",
                        column: x => x.EtimsCodeClassId,
                        principalTable: "EtimsCodeClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EtimsCodeClasses_CdCls",
                table: "EtimsCodeClasses",
                column: "CdCls",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EtimsCodes_EtimsCodeClassId_Cd",
                table: "EtimsCodes",
                columns: new[] { "EtimsCodeClassId", "Cd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EtimsItemClasses_ItemClsCd",
                table: "EtimsItemClasses",
                column: "ItemClsCd",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EtimsItemClasses_ItemClsNm",
                table: "EtimsItemClasses",
                column: "ItemClsNm");

            migrationBuilder.CreateIndex(
                name: "IX_EtimsSyncStates_SyncKey",
                table: "EtimsSyncStates",
                column: "SyncKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EtimsCodes");

            migrationBuilder.DropTable(
                name: "EtimsItemClasses");

            migrationBuilder.DropTable(
                name: "EtimsSyncStates");

            migrationBuilder.DropTable(
                name: "EtimsCodeClasses");
        }
    }
}
