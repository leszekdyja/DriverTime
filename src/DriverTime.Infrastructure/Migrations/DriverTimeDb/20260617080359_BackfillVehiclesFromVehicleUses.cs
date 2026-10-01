using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class BackfillVehiclesFromVehicleUses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS pgcrypto;

                INSERT INTO "Vehicles" ("Id", "CompanyId", "RegistrationNumber", "Vin", "Active", "CreatedAt")
                SELECT
                    gen_random_uuid(),
                    source."CompanyId",
                    source."RegistrationNumber",
                    '',
                    TRUE,
                    NOW()
                FROM (
                    SELECT DISTINCT
                        d."CompanyId",
                        UPPER(TRIM(vu."RegistrationNumber")) AS "RegistrationNumber"
                    FROM "VehicleUses" vu
                    INNER JOIN "DddFiles" d ON d."Id" = vu."DddFileId"
                    WHERE vu."RegistrationNumber" IS NOT NULL
                      AND LENGTH(TRIM(vu."RegistrationNumber")) > 0
                ) source
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "Vehicles" existing
                    WHERE existing."CompanyId" = source."CompanyId"
                      AND existing."RegistrationNumber" = source."RegistrationNumber"
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data backfill is intentionally not reverted to avoid deleting vehicles that may be used later.
        }
    }
}
