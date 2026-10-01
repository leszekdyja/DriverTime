using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverTime.Infrastructure.Migrations.DriverTimeDb
{
    /// <inheritdoc />
    public partial class AddVehicles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('"Vehicles"') IS NULL THEN
                        IF to_regclass('"Vehicle"') IS NOT NULL THEN
                            ALTER TABLE "Vehicle" RENAME TO "Vehicles";
                        ELSE
                            CREATE TABLE "Vehicles" (
                                "Id" uuid NOT NULL,
                                "CompanyId" uuid NOT NULL,
                                "RegistrationNumber" character varying(50) NOT NULL,
                                "Vin" character varying(100) NOT NULL,
                                "Active" boolean NOT NULL,
                                "CreatedAt" timestamp with time zone NOT NULL,
                                CONSTRAINT "PK_Vehicles" PRIMARY KEY ("Id")
                            );
                        END IF;
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'PK_Vehicle'
                    ) THEN
                        ALTER TABLE "Vehicles" RENAME CONSTRAINT "PK_Vehicle" TO "PK_Vehicles";
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'PK_Vehicles'
                    ) THEN
                        ALTER TABLE "Vehicles"
                        ADD CONSTRAINT "PK_Vehicles" PRIMARY KEY ("Id");
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Vehicles"
                ALTER COLUMN "RegistrationNumber" TYPE character varying(50);

                ALTER TABLE "Vehicles"
                ALTER COLUMN "Vin" TYPE character varying(100);

                DROP INDEX IF EXISTS "IX_Vehicle_CompanyId";

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Vehicles_CompanyId_RegistrationNumber"
                ON "Vehicles" ("CompanyId", "RegistrationNumber");
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'FK_DriverActivities_Vehicle_VehicleId'
                    ) THEN
                        ALTER TABLE "DriverActivities"
                        DROP CONSTRAINT "FK_DriverActivities_Vehicle_VehicleId";
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'FK_Vehicle_Companies_CompanyId'
                    ) THEN
                        ALTER TABLE "Vehicles"
                        DROP CONSTRAINT "FK_Vehicle_Companies_CompanyId";
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'FK_Vehicles_Companies_CompanyId'
                    ) THEN
                        ALTER TABLE "Vehicles"
                        ADD CONSTRAINT "FK_Vehicles_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId")
                        REFERENCES "Companies" ("Id")
                        ON DELETE CASCADE;
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'DriverActivities'
                          AND column_name = 'VehicleId'
                    ) AND NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'FK_DriverActivities_Vehicles_VehicleId'
                    ) THEN
                        ALTER TABLE "DriverActivities"
                        ADD CONSTRAINT "FK_DriverActivities_Vehicles_VehicleId"
                        FOREIGN KEY ("VehicleId")
                        REFERENCES "Vehicles" ("Id");
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'FK_DriverActivities_Vehicles_VehicleId'
                    ) THEN
                        ALTER TABLE "DriverActivities"
                        DROP CONSTRAINT "FK_DriverActivities_Vehicles_VehicleId";
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'FK_Vehicles_Companies_CompanyId'
                    ) THEN
                        ALTER TABLE "Vehicles"
                        DROP CONSTRAINT "FK_Vehicles_Companies_CompanyId";
                    END IF;
                END $$;

                DROP INDEX IF EXISTS "IX_Vehicles_CompanyId_RegistrationNumber";
                DROP TABLE IF EXISTS "Vehicles";
                """);
        }
    }
}
