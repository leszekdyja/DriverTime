using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class ScopePlanningDataByOperatingCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OperatingCompanyId",
                table: "PlanningSchedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OperatingCompanyId",
                table: "PlanningDutyBlocks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OperatingCompanyId",
                table: "PlanningDuties",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "PlanningDuties" AS duty
                SET "OperatingCompanyId" = scope."OperatingCompanyId"
                FROM (
                    SELECT assignment."PlanningDutyId", MIN(driver."OperatingCompanyId"::text)::uuid AS "OperatingCompanyId"
                    FROM "PlanningAssignments" AS assignment
                    INNER JOIN "Drivers" AS driver ON driver."Id" = assignment."DriverId"
                    WHERE assignment."PlanningDutyId" IS NOT NULL
                      AND driver."OperatingCompanyId" IS NOT NULL
                    GROUP BY assignment."PlanningDutyId"
                    HAVING COUNT(DISTINCT driver."OperatingCompanyId") = 1
                ) AS scope
                WHERE duty."Id" = scope."PlanningDutyId";
                """);

            migrationBuilder.Sql("""
                UPDATE "PlanningSchedules" AS schedule
                SET "OperatingCompanyId" = scope."OperatingCompanyId"
                FROM (
                    SELECT assignment."PlanningScheduleId", MIN(driver."OperatingCompanyId"::text)::uuid AS "OperatingCompanyId"
                    FROM "PlanningAssignments" AS assignment
                    INNER JOIN "Drivers" AS driver ON driver."Id" = assignment."DriverId"
                    WHERE driver."OperatingCompanyId" IS NOT NULL
                    GROUP BY assignment."PlanningScheduleId"
                    HAVING COUNT(DISTINCT driver."OperatingCompanyId") = 1
                ) AS scope
                WHERE schedule."Id" = scope."PlanningScheduleId";
                """);

            migrationBuilder.Sql("""
                UPDATE "PlanningDutyBlocks" AS block
                SET "OperatingCompanyId" = first_duty."OperatingCompanyId"
                FROM "PlanningDuties" AS first_duty, "PlanningDuties" AS second_duty
                WHERE block."FirstDutyId" = first_duty."Id"
                  AND block."SecondDutyId" = second_duty."Id"
                  AND first_duty."OperatingCompanyId" IS NOT NULL
                  AND first_duty."OperatingCompanyId" = second_duty."OperatingCompanyId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PlanningSchedules_OperatingCompanyId",
                table: "PlanningSchedules",
                column: "OperatingCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDutyBlocks_OperatingCompanyId",
                table: "PlanningDutyBlocks",
                column: "OperatingCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDuties_OperatingCompanyId",
                table: "PlanningDuties",
                column: "OperatingCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlanningDuties_OperatingCompanies_OperatingCompanyId",
                table: "PlanningDuties",
                column: "OperatingCompanyId",
                principalTable: "OperatingCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanningDutyBlocks_OperatingCompanies_OperatingCompanyId",
                table: "PlanningDutyBlocks",
                column: "OperatingCompanyId",
                principalTable: "OperatingCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanningSchedules_OperatingCompanies_OperatingCompanyId",
                table: "PlanningSchedules",
                column: "OperatingCompanyId",
                principalTable: "OperatingCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlanningDuties_OperatingCompanies_OperatingCompanyId",
                table: "PlanningDuties");

            migrationBuilder.DropForeignKey(
                name: "FK_PlanningDutyBlocks_OperatingCompanies_OperatingCompanyId",
                table: "PlanningDutyBlocks");

            migrationBuilder.DropForeignKey(
                name: "FK_PlanningSchedules_OperatingCompanies_OperatingCompanyId",
                table: "PlanningSchedules");

            migrationBuilder.DropIndex(
                name: "IX_PlanningSchedules_OperatingCompanyId",
                table: "PlanningSchedules");

            migrationBuilder.DropIndex(
                name: "IX_PlanningDutyBlocks_OperatingCompanyId",
                table: "PlanningDutyBlocks");

            migrationBuilder.DropIndex(
                name: "IX_PlanningDuties_OperatingCompanyId",
                table: "PlanningDuties");

            migrationBuilder.DropColumn(
                name: "OperatingCompanyId",
                table: "PlanningSchedules");

            migrationBuilder.DropColumn(
                name: "OperatingCompanyId",
                table: "PlanningDutyBlocks");

            migrationBuilder.DropColumn(
                name: "OperatingCompanyId",
                table: "PlanningDuties");
        }
    }
}
