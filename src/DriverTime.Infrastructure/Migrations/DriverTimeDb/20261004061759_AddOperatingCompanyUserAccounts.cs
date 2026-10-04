using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AddOperatingCompanyUserAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OperatingCompanyId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_OperatingCompanyId",
                table: "Users",
                column: "OperatingCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_OperatingCompanies_OperatingCompanyId",
                table: "Users",
                column: "OperatingCompanyId",
                principalTable: "OperatingCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_OperatingCompanies_OperatingCompanyId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_OperatingCompanyId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OperatingCompanyId",
                table: "Users");
        }
    }
}
