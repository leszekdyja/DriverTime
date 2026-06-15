using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AutoCreateDriversFromDddImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Drivers_CompanyId",
                table: "Drivers");

            migrationBuilder.DropIndex(
                name: "IX_DddFiles_CompanyId",
                table: "DddFiles");

            migrationBuilder.AddColumn<DateTime>(
                name: "CardExpiryDate",
                table: "Drivers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardIssuingCountry",
                table: "Drivers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "DriverCreatedDuringImport",
                table: "DddFiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DriverId",
                table: "DddFiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "DddFiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE \"DddFiles\" SET \"FileHash\" = UPPER(MD5(\"Id\"::text)) || UPPER(MD5(\"CompanyId\"::text)) WHERE \"FileHash\" = ''");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_CompanyId_CardNumber",
                table: "Drivers",
                columns: new[] { "CompanyId", "CardNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DddFiles_CompanyId_FileHash",
                table: "DddFiles",
                columns: new[] { "CompanyId", "FileHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DddFiles_DriverId",
                table: "DddFiles",
                column: "DriverId");

            migrationBuilder.AddForeignKey(
                name: "FK_DddFiles_Drivers_DriverId",
                table: "DddFiles",
                column: "DriverId",
                principalTable: "Drivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DddFiles_Drivers_DriverId",
                table: "DddFiles");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_CompanyId_CardNumber",
                table: "Drivers");

            migrationBuilder.DropIndex(
                name: "IX_DddFiles_CompanyId_FileHash",
                table: "DddFiles");

            migrationBuilder.DropIndex(
                name: "IX_DddFiles_DriverId",
                table: "DddFiles");

            migrationBuilder.DropColumn(
                name: "CardExpiryDate",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "CardIssuingCountry",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "DriverCreatedDuringImport",
                table: "DddFiles");

            migrationBuilder.DropColumn(
                name: "DriverId",
                table: "DddFiles");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "DddFiles");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_CompanyId",
                table: "Drivers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_DddFiles_CompanyId",
                table: "DddFiles",
                column: "CompanyId");
        }
    }
}
