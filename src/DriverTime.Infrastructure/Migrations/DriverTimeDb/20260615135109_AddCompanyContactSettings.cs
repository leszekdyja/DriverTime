using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AddCompanyContactSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Companies",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Companies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Companies");
        }
    }
}
