using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AddDddImportRetryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "DddImportMonitoringEntries",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRetryAtUtc",
                table: "DddImportMonitoringEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "DddImportMonitoringEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StoredFilePath",
                table: "DddImportMonitoringEntries",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastError",
                table: "DddImportMonitoringEntries");

            migrationBuilder.DropColumn(
                name: "LastRetryAtUtc",
                table: "DddImportMonitoringEntries");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "DddImportMonitoringEntries");

            migrationBuilder.DropColumn(
                name: "StoredFilePath",
                table: "DddImportMonitoringEntries");
        }
    }
}
