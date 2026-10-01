using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AddDriverPlanningRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PlanningNoDaysOff",
                table: "Drivers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PlanningNoHolidays",
                table: "Drivers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PlanningNoNightDuty",
                table: "Drivers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PlanningNoSaturdays",
                table: "Drivers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PlanningNoWeekends",
                table: "Drivers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlanningNoDaysOff",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "PlanningNoHolidays",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "PlanningNoNightDuty",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "PlanningNoSaturdays",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "PlanningNoWeekends",
                table: "Drivers");
        }
    }
}
