using DriverTime.Application.Common.Interfaces;
using DriverTime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<DriverActivity> DriverActivities => Set<DriverActivity>();

    public DbSet<Violation> Violations => Set<Violation>();

    public DbSet<ImportFile> ImportFiles => Set<ImportFile>();

    public DbSet<TachographFile> TachographFiles => Set<TachographFile>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Company>().ToTable("companies");
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<Driver>().ToTable("drivers");
        modelBuilder.Entity<Vehicle>().ToTable("vehicles");
        modelBuilder.Entity<DriverActivity>().ToTable("driver_activities");
        modelBuilder.Entity<Violation>().ToTable("violations");
        modelBuilder.Entity<ImportFile>().ToTable("import_files");
        modelBuilder.Entity<TachographFile>().ToTable("tachograph_files");
        modelBuilder.Entity<Notification>().ToTable("notifications");
        modelBuilder.Entity<AuditLog>().ToTable("audit_log");
    }
}