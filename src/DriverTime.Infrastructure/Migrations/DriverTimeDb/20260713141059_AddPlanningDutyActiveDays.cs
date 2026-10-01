using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AddPlanningDutyActiveDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveDaysMask",
                table: "PlanningDuties",
                type: "integer",
                nullable: true,
                defaultValue: 31);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeHolidays",
                table: "PlanningDuties",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveDaysMask",
                table: "PlanningDuties");

            migrationBuilder.DropColumn(
                name: "IncludeHolidays",
                table: "PlanningDuties");
        }
    }
}
