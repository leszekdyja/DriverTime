using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDddEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_driver_activities_drivers_DriverId",
                table: "driver_activities");

            migrationBuilder.DropForeignKey(
                name: "FK_driver_activities_import_files_ImportFileId",
                table: "driver_activities");

            migrationBuilder.DropForeignKey(
                name: "FK_driver_activities_vehicles_VehicleId",
                table: "driver_activities");

            migrationBuilder.DropForeignKey(
                name: "FK_drivers_companies_CompanyId",
                table: "drivers");

            migrationBuilder.DropForeignKey(
                name: "FK_import_files_companies_CompanyId",
                table: "import_files");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_companies_CompanyId",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_users_companies_CompanyId",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_vehicles_companies_CompanyId",
                table: "vehicles");

            migrationBuilder.DropForeignKey(
                name: "FK_violations_drivers_DriverId",
                table: "violations");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "tachograph_files");

            migrationBuilder.DropPrimaryKey(
                name: "PK_companies",
                table: "companies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_violations",
                table: "violations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_vehicles",
                table: "vehicles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_notifications",
                table: "notifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_import_files",
                table: "import_files");

            migrationBuilder.DropPrimaryKey(
                name: "PK_drivers",
                table: "drivers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_driver_activities",
                table: "driver_activities");

            migrationBuilder.RenameTable(
                name: "companies",
                newName: "Companies");

            migrationBuilder.RenameTable(
                name: "violations",
                newName: "Violation");

            migrationBuilder.RenameTable(
                name: "vehicles",
                newName: "Vehicle");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "User");

            migrationBuilder.RenameTable(
                name: "notifications",
                newName: "Notification");

            migrationBuilder.RenameTable(
                name: "import_files",
                newName: "ImportFile");

            migrationBuilder.RenameTable(
                name: "drivers",
                newName: "Driver");

            migrationBuilder.RenameTable(
                name: "driver_activities",
                newName: "DriverActivities");

            migrationBuilder.RenameIndex(
                name: "IX_violations_DriverId",
                table: "Violation",
                newName: "IX_Violation_DriverId");

            migrationBuilder.RenameIndex(
                name: "IX_vehicles_CompanyId",
                table: "Vehicle",
                newName: "IX_Vehicle_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_users_CompanyId",
                table: "User",
                newName: "IX_User_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_notifications_CompanyId",
                table: "Notification",
                newName: "IX_Notification_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_import_files_CompanyId",
                table: "ImportFile",
                newName: "IX_ImportFile_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_drivers_CompanyId",
                table: "Driver",
                newName: "IX_Driver_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_driver_activities_VehicleId",
                table: "DriverActivities",
                newName: "IX_DriverActivities_VehicleId");

            migrationBuilder.RenameIndex(
                name: "IX_driver_activities_ImportFileId",
                table: "DriverActivities",
                newName: "IX_DriverActivities_ImportFileId");

            migrationBuilder.RenameIndex(
                name: "IX_driver_activities_DriverId",
                table: "DriverActivities",
                newName: "IX_DriverActivities_DriverId");

            migrationBuilder.AddColumn<Guid>(
                name: "DddFileId",
                table: "DriverActivities",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Companies",
                table: "Companies",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Violation",
                table: "Violation",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Vehicle",
                table: "Vehicle",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_User",
                table: "User",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Notification",
                table: "Notification",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ImportFile",
                table: "ImportFile",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Driver",
                table: "Driver",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DriverActivities",
                table: "DriverActivities",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "DddFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    DriverName = table.Column<string>(type: "text", nullable: true),
                    DriverCardNumber = table.Column<string>(type: "text", nullable: true),
                    VehicleRegistrationNumber = table.Column<string>(type: "text", nullable: true),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DddFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CountryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DddFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    EntryTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CountryEntries_DddFiles_DddFileId",
                        column: x => x.DddFileId,
                        principalTable: "DddFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleUses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DddFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleRegistrationNumber = table.Column<string>(type: "text", nullable: true),
                    StartTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleUses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleUses_DddFiles_DddFileId",
                        column: x => x.DddFileId,
                        principalTable: "DddFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverActivities_DddFileId",
                table: "DriverActivities",
                column: "DddFileId");

            migrationBuilder.CreateIndex(
                name: "IX_CountryEntries_DddFileId",
                table: "CountryEntries",
                column: "DddFileId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleUses_DddFileId",
                table: "VehicleUses",
                column: "DddFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Driver_Companies_CompanyId",
                table: "Driver",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DriverActivities_DddFiles_DddFileId",
                table: "DriverActivities",
                column: "DddFileId",
                principalTable: "DddFiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DriverActivities_Driver_DriverId",
                table: "DriverActivities",
                column: "DriverId",
                principalTable: "Driver",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DriverActivities_ImportFile_ImportFileId",
                table: "DriverActivities",
                column: "ImportFileId",
                principalTable: "ImportFile",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DriverActivities_Vehicle_VehicleId",
                table: "DriverActivities",
                column: "VehicleId",
                principalTable: "Vehicle",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ImportFile_Companies_CompanyId",
                table: "ImportFile",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notification_Companies_CompanyId",
                table: "Notification",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_User_Companies_CompanyId",
                table: "User",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicle_Companies_CompanyId",
                table: "Vehicle",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Violation_Driver_DriverId",
                table: "Violation",
                column: "DriverId",
                principalTable: "Driver",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Driver_Companies_CompanyId",
                table: "Driver");

            migrationBuilder.DropForeignKey(
                name: "FK_DriverActivities_DddFiles_DddFileId",
                table: "DriverActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_DriverActivities_Driver_DriverId",
                table: "DriverActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_DriverActivities_ImportFile_ImportFileId",
                table: "DriverActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_DriverActivities_Vehicle_VehicleId",
                table: "DriverActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_ImportFile_Companies_CompanyId",
                table: "ImportFile");

            migrationBuilder.DropForeignKey(
                name: "FK_Notification_Companies_CompanyId",
                table: "Notification");

            migrationBuilder.DropForeignKey(
                name: "FK_User_Companies_CompanyId",
                table: "User");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicle_Companies_CompanyId",
                table: "Vehicle");

            migrationBuilder.DropForeignKey(
                name: "FK_Violation_Driver_DriverId",
                table: "Violation");

            migrationBuilder.DropTable(
                name: "CountryEntries");

            migrationBuilder.DropTable(
                name: "VehicleUses");

            migrationBuilder.DropTable(
                name: "DddFiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Companies",
                table: "Companies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Violation",
                table: "Violation");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Vehicle",
                table: "Vehicle");

            migrationBuilder.DropPrimaryKey(
                name: "PK_User",
                table: "User");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Notification",
                table: "Notification");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ImportFile",
                table: "ImportFile");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DriverActivities",
                table: "DriverActivities");

            migrationBuilder.DropIndex(
                name: "IX_DriverActivities_DddFileId",
                table: "DriverActivities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Driver",
                table: "Driver");

            migrationBuilder.DropColumn(
                name: "DddFileId",
                table: "DriverActivities");

            migrationBuilder.RenameTable(
                name: "Companies",
                newName: "companies");

            migrationBuilder.RenameTable(
                name: "Violation",
                newName: "violations");

            migrationBuilder.RenameTable(
                name: "Vehicle",
                newName: "vehicles");

            migrationBuilder.RenameTable(
                name: "User",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "Notification",
                newName: "notifications");

            migrationBuilder.RenameTable(
                name: "ImportFile",
                newName: "import_files");

            migrationBuilder.RenameTable(
                name: "DriverActivities",
                newName: "driver_activities");

            migrationBuilder.RenameTable(
                name: "Driver",
                newName: "drivers");

            migrationBuilder.RenameIndex(
                name: "IX_Violation_DriverId",
                table: "violations",
                newName: "IX_violations_DriverId");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicle_CompanyId",
                table: "vehicles",
                newName: "IX_vehicles_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_User_CompanyId",
                table: "users",
                newName: "IX_users_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_Notification_CompanyId",
                table: "notifications",
                newName: "IX_notifications_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_ImportFile_CompanyId",
                table: "import_files",
                newName: "IX_import_files_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_DriverActivities_VehicleId",
                table: "driver_activities",
                newName: "IX_driver_activities_VehicleId");

            migrationBuilder.RenameIndex(
                name: "IX_DriverActivities_ImportFileId",
                table: "driver_activities",
                newName: "IX_driver_activities_ImportFileId");

            migrationBuilder.RenameIndex(
                name: "IX_DriverActivities_DriverId",
                table: "driver_activities",
                newName: "IX_driver_activities_DriverId");

            migrationBuilder.RenameIndex(
                name: "IX_Driver_CompanyId",
                table: "drivers",
                newName: "IX_drivers_CompanyId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_companies",
                table: "companies",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_violations",
                table: "violations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_vehicles",
                table: "vehicles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                table: "users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_notifications",
                table: "notifications",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_import_files",
                table: "import_files",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_driver_activities",
                table: "driver_activities",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_drivers",
                table: "drivers",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_log_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "tachograph_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileType = table.Column<string>(type: "text", nullable: false),
                    ParserStatus = table.Column<string>(type: "text", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tachograph_files", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_UserId",
                table: "audit_log",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_driver_activities_drivers_DriverId",
                table: "driver_activities",
                column: "DriverId",
                principalTable: "drivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_driver_activities_import_files_ImportFileId",
                table: "driver_activities",
                column: "ImportFileId",
                principalTable: "import_files",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_driver_activities_vehicles_VehicleId",
                table: "driver_activities",
                column: "VehicleId",
                principalTable: "vehicles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_drivers_companies_CompanyId",
                table: "drivers",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_import_files_companies_CompanyId",
                table: "import_files",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_companies_CompanyId",
                table: "notifications",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_users_companies_CompanyId",
                table: "users",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_vehicles_companies_CompanyId",
                table: "vehicles",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_violations_drivers_DriverId",
                table: "violations",
                column: "DriverId",
                principalTable: "drivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
