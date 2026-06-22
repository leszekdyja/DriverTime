using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AddVehicleUseOdometerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DistanceKm",
                table: "VehicleUses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EndOdometerKm",
                table: "VehicleUses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartOdometerKm",
                table: "VehicleUses",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DistanceKm",
                table: "VehicleUses");

            migrationBuilder.DropColumn(
                name: "EndOdometerKm",
                table: "VehicleUses");

            migrationBuilder.DropColumn(
                name: "StartOdometerKm",
                table: "VehicleUses");
        }
    }
}
