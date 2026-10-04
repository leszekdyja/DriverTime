using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AddOperatingCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OperatingCompanyId",
                table: "Drivers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OperatingCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaxNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingCompanies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatingCompanies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_OperatingCompanyId",
                table: "Drivers",
                column: "OperatingCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingCompanies_CompanyId_Name",
                table: "OperatingCompanies",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Drivers_OperatingCompanies_OperatingCompanyId",
                table: "Drivers",
                column: "OperatingCompanyId",
                principalTable: "OperatingCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Drivers_OperatingCompanies_OperatingCompanyId",
                table: "Drivers");

            migrationBuilder.DropTable(
                name: "OperatingCompanies");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_OperatingCompanyId",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "OperatingCompanyId",
                table: "Drivers");
        }
    }
}
