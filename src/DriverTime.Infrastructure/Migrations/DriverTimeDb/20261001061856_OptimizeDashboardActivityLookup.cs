using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class OptimizeDashboardActivityLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DriverActivities_EndUtc_StartUtc_DddFileId",
                table: "DriverActivities",
                columns: new[] { "EndUtc", "StartUtc", "DddFileId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DriverActivities_EndUtc_StartUtc_DddFileId",
                table: "DriverActivities");
        }
    }
}
