using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AddPlanningDutyBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanningDutyBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstDutyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecondDutyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredVehicleType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningDutyBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningDutyBlocks_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanningDutyBlocks_PlanningDuties_FirstDutyId",
                        column: x => x.FirstDutyId,
                        principalTable: "PlanningDuties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanningDutyBlocks_PlanningDuties_SecondDutyId",
                        column: x => x.SecondDutyId,
                        principalTable: "PlanningDuties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDutyBlocks_CompanyId",
                table: "PlanningDutyBlocks",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDutyBlocks_CompanyId_FirstDutyId_SecondDutyId",
                table: "PlanningDutyBlocks",
                columns: new[] { "CompanyId", "FirstDutyId", "SecondDutyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDutyBlocks_FirstDutyId",
                table: "PlanningDutyBlocks",
                column: "FirstDutyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDutyBlocks_SecondDutyId",
                table: "PlanningDutyBlocks",
                column: "SecondDutyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanningDutyBlocks");
        }
    }
}
