using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AddPlanningDriverPairs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanningDriverPairs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstDriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecondDriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsNightDutyPair = table.Column<bool>(type: "boolean", nullable: false),
                    PreventSameShift = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningDriverPairs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningDriverPairs_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanningDriverPairs_Drivers_FirstDriverId",
                        column: x => x.FirstDriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanningDriverPairs_Drivers_SecondDriverId",
                        column: x => x.SecondDriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDriverPairs_CompanyId",
                table: "PlanningDriverPairs",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDriverPairs_CompanyId_FirstDriverId_SecondDriverId_~",
                table: "PlanningDriverPairs",
                columns: new[] { "CompanyId", "FirstDriverId", "SecondDriverId", "IsNightDutyPair" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDriverPairs_FirstDriverId",
                table: "PlanningDriverPairs",
                column: "FirstDriverId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDriverPairs_SecondDriverId",
                table: "PlanningDriverPairs",
                column: "SecondDriverId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanningDriverPairs");
        }
    }
}
