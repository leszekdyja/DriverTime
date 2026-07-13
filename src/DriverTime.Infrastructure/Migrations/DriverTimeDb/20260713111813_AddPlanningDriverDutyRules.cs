using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AddPlanningDriverDutyRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanningDriverDutyRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanningDutyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningDriverDutyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningDriverDutyRules_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanningDriverDutyRules_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanningDriverDutyRules_PlanningDuties_PlanningDutyId",
                        column: x => x.PlanningDutyId,
                        principalTable: "PlanningDuties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDriverDutyRules_CompanyId",
                table: "PlanningDriverDutyRules",
                column: "CompanyId");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_PlanningDriverDutyRules_CompanyId_DriverId_PlanningDutyId_Type_ValidFrom_ValidTo"
                ON "PlanningDriverDutyRules" ("CompanyId", "DriverId", "PlanningDutyId", "Type", "ValidFrom", "ValidTo")
                NULLS NOT DISTINCT;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDriverDutyRules_DriverId",
                table: "PlanningDriverDutyRules",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDriverDutyRules_PlanningDutyId",
                table: "PlanningDriverDutyRules",
                column: "PlanningDutyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDriverDutyRules_Type",
                table: "PlanningDriverDutyRules",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanningDriverDutyRules");
        }
    }
}

