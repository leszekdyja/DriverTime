using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class CleanupVehicleRegistrationVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'DriverActivities'
                          AND column_name = 'VehicleId'
                    ) THEN
                        WITH normalized AS (
                            SELECT
                                "Id",
                                "CompanyId",
                                UPPER(REPLACE("RegistrationNumber", ' ', '')) AS compact_registration,
                                ROW_NUMBER() OVER (
                                    PARTITION BY "CompanyId", UPPER(REPLACE("RegistrationNumber", ' ', ''))
                                    ORDER BY "CreatedAt", "Id"
                                ) AS duplicate_rank
                            FROM "Vehicles"
                        ),
                        victims AS (
                            SELECT n."Id"
                            FROM normalized n
                            WHERE LENGTH(n.compact_registration) < 5
                               OR n.duplicate_rank > 1
                               OR EXISTS (
                                   SELECT 1
                                   FROM normalized fuller
                                   WHERE fuller."CompanyId" = n."CompanyId"
                                     AND fuller."Id" <> n."Id"
                                     AND LENGTH(fuller.compact_registration) > LENGTH(n.compact_registration)
                                     AND RIGHT(fuller.compact_registration, LENGTH(n.compact_registration)) = n.compact_registration
                               )
                        )
                        UPDATE "DriverActivities"
                        SET "VehicleId" = NULL
                        WHERE "VehicleId" IN (SELECT "Id" FROM victims);
                    END IF;
                END $$;

                WITH normalized AS (
                    SELECT
                        "Id",
                        "CompanyId",
                        UPPER(REPLACE("RegistrationNumber", ' ', '')) AS compact_registration,
                        ROW_NUMBER() OVER (
                            PARTITION BY "CompanyId", UPPER(REPLACE("RegistrationNumber", ' ', ''))
                            ORDER BY "CreatedAt", "Id"
                        ) AS duplicate_rank
                    FROM "Vehicles"
                ),
                victims AS (
                    SELECT n."Id"
                    FROM normalized n
                    WHERE LENGTH(n.compact_registration) < 5
                       OR n.duplicate_rank > 1
                       OR EXISTS (
                           SELECT 1
                           FROM normalized fuller
                           WHERE fuller."CompanyId" = n."CompanyId"
                             AND fuller."Id" <> n."Id"
                             AND LENGTH(fuller.compact_registration) > LENGTH(n.compact_registration)
                             AND RIGHT(fuller.compact_registration, LENGTH(n.compact_registration)) = n.compact_registration
                       )
                )
                DELETE FROM "Vehicles"
                WHERE "Id" IN (SELECT "Id" FROM victims);
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cleanup is intentionally not reverted because removed rows are derived duplicates.
        }
    }
}
