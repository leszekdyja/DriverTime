using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AddPlanningSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanningDuties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DutyNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    VehicleRequirement = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TotalDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    WorkMinutes = table.Column<int>(type: "integer", nullable: true),
                    BreakMinutes = table.Column<int>(type: "integer", nullable: true),
                    DrivingMinutes = table.Column<int>(type: "integer", nullable: true),
                    DistanceKm = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SourceFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningDuties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningDuties_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanningSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningSchedules_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanningDutyLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanningDutyId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Variant = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DistanceKm = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningDutyLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningDutyLines_PlanningDuties_PlanningDutyId",
                        column: x => x.PlanningDutyId,
                        principalTable: "PlanningDuties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanningDutyStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanningDutyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    StopName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Km = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    TripGroup = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ArrivalTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    DepartureTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    LineCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningDutyStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningDutyStops_PlanningDuties_PlanningDutyId",
                        column: x => x.PlanningDutyId,
                        principalTable: "PlanningDuties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanningAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanningScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanningDutyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    AssignmentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningAssignments_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanningAssignments_PlanningDuties_PlanningDutyId",
                        column: x => x.PlanningDutyId,
                        principalTable: "PlanningDuties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanningAssignments_PlanningSchedules_PlanningScheduleId",
                        column: x => x.PlanningScheduleId,
                        principalTable: "PlanningSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningAssignments_CompanyId_Date",
                table: "PlanningAssignments",
                columns: new[] { "CompanyId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningAssignments_DriverId",
                table: "PlanningAssignments",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningAssignments_PlanningDutyId",
                table: "PlanningAssignments",
                column: "PlanningDutyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningAssignments_PlanningScheduleId_DriverId_Date",
                table: "PlanningAssignments",
                columns: new[] { "PlanningScheduleId", "DriverId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDuties_CompanyId_DutyNumber_ValidFrom",
                table: "PlanningDuties",
                columns: new[] { "CompanyId", "DutyNumber", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDutyLines_PlanningDutyId",
                table: "PlanningDutyLines",
                column: "PlanningDutyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDutyStops_PlanningDutyId_Sequence",
                table: "PlanningDutyStops",
                columns: new[] { "PlanningDutyId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningSchedules_CompanyId_Year_Month",
                table: "PlanningSchedules",
                columns: new[] { "CompanyId", "Year", "Month" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanningAssignments");

            migrationBuilder.DropTable(
                name: "PlanningDutyLines");

            migrationBuilder.DropTable(
                name: "PlanningDutyStops");

            migrationBuilder.DropTable(
                name: "PlanningSchedules");

            migrationBuilder.DropTable(
                name: "PlanningDuties");
        }
    }
}
