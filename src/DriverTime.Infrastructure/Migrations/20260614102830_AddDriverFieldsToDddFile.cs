using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverFieldsToDddFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.DropTable(
                name: "ImportFile");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "Vehicle");

            migrationBuilder.DropTable(
                name: "Violation");

            migrationBuilder.DropTable(
                name: "Driver");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_DriverActivities_DriverId",
                table: "DriverActivities");

            migrationBuilder.DropIndex(
                name: "IX_DriverActivities_ImportFileId",
                table: "DriverActivities");

            migrationBuilder.DropIndex(
                name: "IX_DriverActivities_VehicleId",
                table: "DriverActivities");

            migrationBuilder.DropColumn(
                name: "EndTimeUtc",
                table: "VehicleUses");

            migrationBuilder.DropColumn(
                name: "StartTimeUtc",
                table: "VehicleUses");

            migrationBuilder.DropColumn(
                name: "VehicleRegistrationNumber",
                table: "VehicleUses");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "DriverActivities");

            migrationBuilder.DropColumn(
                name: "DriverId",
                table: "DriverActivities");

            migrationBuilder.DropColumn(
                name: "ImportFileId",
                table: "DriverActivities");

            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "DriverActivities");

            migrationBuilder.DropColumn(
                name: "DriverName",
                table: "DddFiles");

            migrationBuilder.DropColumn(
                name: "VehicleRegistrationNumber",
                table: "DddFiles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CountryEntries");

            migrationBuilder.RenameColumn(
                name: "StartTime",
                table: "DriverActivities",
                newName: "StartUtc");

            migrationBuilder.RenameColumn(
                name: "EndTime",
                table: "DriverActivities",
                newName: "EndUtc");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndUtc",
                table: "VehicleUses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                table: "VehicleUses",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartUtc",
                table: "VehicleUses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<Guid>(
                name: "DddFileId",
                table: "DriverActivities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DriverCardNumber",
                table: "DddFiles",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverFirstName",
                table: "DddFiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DriverLastName",
                table: "DddFiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_DriverActivities_DddFiles_DddFileId",
                table: "DriverActivities",
                column: "DddFileId",
                principalTable: "DddFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DriverActivities_DddFiles_DddFileId",
                table: "DriverActivities");

            migrationBuilder.DropColumn(
                name: "EndUtc",
                table: "VehicleUses");

            migrationBuilder.DropColumn(
                name: "RegistrationNumber",
                table: "VehicleUses");

            migrationBuilder.DropColumn(
                name: "StartUtc",
                table: "VehicleUses");

            migrationBuilder.DropColumn(
                name: "DriverFirstName",
                table: "DddFiles");

            migrationBuilder.DropColumn(
                name: "DriverLastName",
                table: "DddFiles");

            migrationBuilder.RenameColumn(
                name: "StartUtc",
                table: "DriverActivities",
                newName: "StartTime");

            migrationBuilder.RenameColumn(
                name: "EndUtc",
                table: "DriverActivities",
                newName: "EndTime");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndTimeUtc",
                table: "VehicleUses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartTimeUtc",
                table: "VehicleUses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleRegistrationNumber",
                table: "VehicleUses",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DddFileId",
                table: "DriverActivities",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "DriverActivities",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "DriverId",
                table: "DriverActivities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ImportFileId",
                table: "DriverActivities",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleId",
                table: "DriverActivities",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DriverCardNumber",
                table: "DddFiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "DriverName",
                table: "DddFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleRegistrationNumber",
                table: "DddFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CountryEntries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    VatNumber = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Driver",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DriverCardNumber = table.Column<string>(type: "text", nullable: false),
                    DrivingLicenseNumber = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Driver", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Driver_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportFile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StoredFileName = table.Column<string>(type: "text", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportFile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportFile_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notification_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                    table.ForeignKey(
                        name: "FK_User_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vehicle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "text", nullable: false),
                    Vin = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicle_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Violation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    RegulationReference = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    ViolationEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ViolationStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ViolationType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Violation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Violation_Driver_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Driver",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverActivities_DriverId",
                table: "DriverActivities",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverActivities_ImportFileId",
                table: "DriverActivities",
                column: "ImportFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverActivities_VehicleId",
                table: "DriverActivities",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Driver_CompanyId",
                table: "Driver",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportFile_CompanyId",
                table: "ImportFile",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_CompanyId",
                table: "Notification",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_User_CompanyId",
                table: "User",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_CompanyId",
                table: "Vehicle",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Violation_DriverId",
                table: "Violation",
                column: "DriverId");

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
        }
    }
}
