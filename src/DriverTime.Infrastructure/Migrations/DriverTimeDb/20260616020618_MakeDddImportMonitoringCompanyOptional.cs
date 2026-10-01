using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class MakeDddImportMonitoringCompanyOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DddImportMonitoringEntries_Companies_CompanyId",
                table: "DddImportMonitoringEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_DddImportMonitoringEntries_Users_UserId",
                table: "DddImportMonitoringEntries");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "DddImportMonitoringEntries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "DddImportMonitoringEntries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_DddImportMonitoringEntries_Companies_CompanyId",
                table: "DddImportMonitoringEntries",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DddImportMonitoringEntries_Users_UserId",
                table: "DddImportMonitoringEntries",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DddImportMonitoringEntries_Companies_CompanyId",
                table: "DddImportMonitoringEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_DddImportMonitoringEntries_Users_UserId",
                table: "DddImportMonitoringEntries");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "DddImportMonitoringEntries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "DddImportMonitoringEntries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DddImportMonitoringEntries_Companies_CompanyId",
                table: "DddImportMonitoringEntries",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DddImportMonitoringEntries_Users_UserId",
                table: "DddImportMonitoringEntries",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
