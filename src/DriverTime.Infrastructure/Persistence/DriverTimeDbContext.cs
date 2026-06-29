using DriverTime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Persistence;

public class DriverTimeDbContext : DbContext
{
    public DriverTimeDbContext(DbContextOptions<DriverTimeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<DddFile> DddFiles => Set<DddFile>();

    public DbSet<DddImportMonitoringEntry> DddImportMonitoringEntries =>
        Set<DddImportMonitoringEntry>();

    public DbSet<DriverActivity> DriverActivities => Set<DriverActivity>();

    public DbSet<VehicleUse> VehicleUses => Set<VehicleUse>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<CountryEntry> CountryEntries => Set<CountryEntry>();

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<Violation> Violations => Set<Violation>();

    public DbSet<ComplianceRun> ComplianceRuns => Set<ComplianceRun>();

    public DbSet<ComplianceRunViolation> ComplianceRunViolations =>
        Set<ComplianceRunViolation>();

    public DbSet<CardReadSession> CardReadSessions => Set<CardReadSession>();

    public DbSet<PlanningDuty> PlanningDuties => Set<PlanningDuty>();

    public DbSet<PlanningDutyLine> PlanningDutyLines => Set<PlanningDutyLine>();

    public DbSet<PlanningDutyStop> PlanningDutyStops => Set<PlanningDutyStop>();

    public DbSet<PlanningSchedule> PlanningSchedules => Set<PlanningSchedule>();

    public DbSet<PlanningAssignment> PlanningAssignments => Set<PlanningAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(200);

            entity.Property(x => x.VatNumber)
                .HasMaxLength(50);

            entity.Property(x => x.Address)
                .HasMaxLength(500);

            entity.Property(x => x.Email)
                .HasMaxLength(320);

            entity.Property(x => x.Phone)
                .HasMaxLength(50);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.FirstName).HasMaxLength(100);
            entity.Property(x => x.LastName).HasMaxLength(100);
            entity.Property(x => x.PasswordHash).HasMaxLength(500);
            entity.HasOne(x => x.Company)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.CompanyId);
            entity.HasOne(x => x.Role)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<DddFile>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CompanyId, x.FileHash }).IsUnique();

            entity.Property(x => x.FileName)
                .HasMaxLength(500);

            entity.Property(x => x.DriverFirstName)
                .HasMaxLength(200);

            entity.Property(x => x.DriverLastName)
                .HasMaxLength(200);

            entity.Property(x => x.FileHash)
                .HasMaxLength(64);

            entity.HasOne(x => x.Company)
                .WithMany(x => x.DddFiles)
                .HasForeignKey(x => x.CompanyId);

            entity.HasOne(x => x.Driver)
                .WithMany(x => x.DddFiles)
                .HasForeignKey(x => x.DriverId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DddImportMonitoringEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CompanyId, x.CreatedAtUtc });
            entity.HasIndex(x => x.UserId);

            entity.Property(x => x.FileName)
                .HasMaxLength(500);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.Property(x => x.ErrorMessage)
                .HasMaxLength(4000);

            entity.Property(x => x.LastError)
                .HasMaxLength(4000);

            entity.Property(x => x.StoredFilePath)
                .HasMaxLength(1000);

            entity.HasOne(x => x.Company)
                .WithMany(x => x.DddImportMonitoringEntries)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DriverActivity>(entity =>
        {
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<VehicleUse>(entity =>
        {
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.ToTable("Vehicles");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.CompanyId, x.RegistrationNumber })
                .IsUnique();

            entity.Property(x => x.RegistrationNumber)
                .HasMaxLength(50);

            entity.Property(x => x.Vin)
                .HasMaxLength(100);

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Vehicles)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CountryEntry>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.EntryType)
                .HasMaxLength(20)
                .HasDefaultValue("Unknown");
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CompanyId, x.CardNumber }).IsUnique();

            entity.Property(x => x.FirstName)
                .HasMaxLength(100);

            entity.Property(x => x.LastName)
                .HasMaxLength(100);

            entity.Property(x => x.CardNumber)
                .HasMaxLength(100);

            entity.Property(x => x.CardIssuingCountry)
                .HasMaxLength(10);

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Drivers)
                .HasForeignKey(x => x.CompanyId);
        });

        modelBuilder.Entity<Violation>(entity =>
        {
            entity.ToTable("violations");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.DriverId);

            entity.Property(x => x.MetadataJson)
                .HasMaxLength(8000);

            entity.HasOne(x => x.Driver)
                .WithMany()
                .HasForeignKey(x => x.DriverId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ComplianceRun>(entity =>
        {
            entity.ToTable("compliance_runs");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.CompanyId, x.DriverId, x.CreatedAtUtc });

            entity.Property(x => x.Trigger)
                .HasMaxLength(100);

            entity.HasOne(x => x.Company)
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Driver)
                .WithMany()
                .HasForeignKey(x => x.DriverId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Violations)
                .WithOne(x => x.ComplianceRun)
                .HasForeignKey(x => x.ComplianceRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ComplianceRunViolation>(entity =>
        {
            entity.ToTable("compliance_run_violations");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.ComplianceRunId);

            entity.Property(x => x.Code)
                .HasMaxLength(200);

            entity.Property(x => x.RuleName)
                .HasMaxLength(300);

            entity.Property(x => x.Severity)
                .HasMaxLength(50);

            entity.Property(x => x.Description)
                .HasMaxLength(4000);

            entity.Property(x => x.MetadataJson)
                .HasMaxLength(8000);
        });


        modelBuilder.Entity<PlanningDuty>(entity =>
        {
            entity.ToTable("PlanningDuties");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CompanyId, x.DutyNumber, x.ValidFrom });

            entity.Property(x => x.DutyNumber).HasMaxLength(50);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.VehicleRequirement).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.Property(x => x.SourceFileName).HasMaxLength(500);
            entity.Property(x => x.DistanceKm).HasPrecision(10, 2);

            entity.HasOne(x => x.Company)
                .WithMany(x => x.PlanningDuties)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Lines)
                .WithOne(x => x.PlanningDuty)
                .HasForeignKey(x => x.PlanningDutyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Stops)
                .WithOne(x => x.PlanningDuty)
                .HasForeignKey(x => x.PlanningDutyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlanningDutyLine>(entity =>
        {
            entity.ToTable("PlanningDutyLines");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PlanningDutyId);

            entity.Property(x => x.LineCode).HasMaxLength(50);
            entity.Property(x => x.Variant).HasMaxLength(100);
            entity.Property(x => x.DistanceKm).HasPrecision(10, 2);
        });

        modelBuilder.Entity<PlanningDutyStop>(entity =>
        {
            entity.ToTable("PlanningDutyStops");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PlanningDutyId, x.Sequence });

            entity.Property(x => x.StopName).HasMaxLength(200);
            entity.Property(x => x.TripGroup).HasMaxLength(100);
            entity.Property(x => x.LineCode).HasMaxLength(50);
            entity.Property(x => x.Km).HasPrecision(10, 2);
        });

        modelBuilder.Entity<PlanningSchedule>(entity =>
        {
            entity.ToTable("PlanningSchedules");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CompanyId, x.Year, x.Month });

            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(4000);

            entity.HasOne(x => x.Company)
                .WithMany(x => x.PlanningSchedules)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Assignments)
                .WithOne(x => x.PlanningSchedule)
                .HasForeignKey(x => x.PlanningScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlanningAssignment>(entity =>
        {
            entity.ToTable("PlanningAssignments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PlanningScheduleId, x.DriverId, x.Date }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.Date });

            entity.Property(x => x.AssignmentType)
                .HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(x => x.Notes).HasMaxLength(2000);

            entity.HasOne(x => x.Driver)
                .WithMany(x => x.PlanningAssignments)
                .HasForeignKey(x => x.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PlanningDuty)
                .WithMany(x => x.PlanningAssignments)
                .HasForeignKey(x => x.PlanningDutyId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CardReadSession>(entity =>
        {
            entity.ToTable("CardReadSessions");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.CompanyId, x.StartedAtUtc });
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Status)
                .HasMaxLength(32);

            entity.Property(x => x.ReaderName)
                .HasMaxLength(200);

            entity.Property(x => x.DriverCardNumber)
                .HasMaxLength(100);

            entity.Property(x => x.ErrorMessage)
                .HasMaxLength(2000);

            entity.Property(x => x.Notes)
                .HasMaxLength(1000);

            entity.HasOne(x => x.Company)
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}


