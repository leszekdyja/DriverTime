using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AddVehicleUnitDddImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileType",
                table: "DddFiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "driver_card");

            migrationBuilder.AddColumn<Guid>(
                name: "OperatingCompanyId",
                table: "DddFiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParsedVehicleUnitJson",
                table: "DddFiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VehicleIdentificationNumber",
                table: "DddFiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VehicleRegistrationNumber",
                table: "DddFiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DddFiles_OperatingCompanyId",
                table: "DddFiles",
                column: "OperatingCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_DddFiles_OperatingCompanies_OperatingCompanyId",
                table: "DddFiles",
                column: "OperatingCompanyId",
                principalTable: "OperatingCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DddFiles_OperatingCompanies_OperatingCompanyId",
                table: "DddFiles");

            migrationBuilder.DropIndex(
                name: "IX_DddFiles_OperatingCompanyId",
                table: "DddFiles");

            migrationBuilder.DropColumn(
                name: "FileType",
                table: "DddFiles");

            migrationBuilder.DropColumn(
                name: "OperatingCompanyId",
                table: "DddFiles");

            migrationBuilder.DropColumn(
                name: "ParsedVehicleUnitJson",
                table: "DddFiles");

            migrationBuilder.DropColumn(
                name: "VehicleIdentificationNumber",
                table: "DddFiles");

            migrationBuilder.DropColumn(
                name: "VehicleRegistrationNumber",
                table: "DddFiles");
        }
    }
}
