using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AddVehicleOperatingCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OperatingCompanyId",
                table: "Vehicles",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Vehicles" AS vehicle
                SET "OperatingCompanyId" = scope."OperatingCompanyId"
                FROM (
                    SELECT candidate."Id", MIN(driver."OperatingCompanyId"::text)::uuid AS "OperatingCompanyId"
                    FROM "Vehicles" AS candidate
                    INNER JOIN "VehicleUses" AS vehicle_use
                        ON UPPER(REPLACE(candidate."RegistrationNumber", ' ', ''))
                           LIKE '%' || UPPER(REPLACE(vehicle_use."RegistrationNumber", ' ', ''))
                    INNER JOIN "DddFiles" AS ddd_file ON ddd_file."Id" = vehicle_use."DddFileId"
                    INNER JOIN "Drivers" AS driver ON driver."Id" = ddd_file."DriverId"
                    WHERE driver."OperatingCompanyId" IS NOT NULL
                    GROUP BY candidate."Id"
                    HAVING COUNT(DISTINCT driver."OperatingCompanyId") = 1
                ) AS scope
                WHERE vehicle."Id" = scope."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_OperatingCompanyId",
                table: "Vehicles",
                column: "OperatingCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_OperatingCompanies_OperatingCompanyId",
                table: "Vehicles",
                column: "OperatingCompanyId",
                principalTable: "OperatingCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_OperatingCompanies_OperatingCompanyId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_OperatingCompanyId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "OperatingCompanyId",
                table: "Vehicles");
        }
    }
}
