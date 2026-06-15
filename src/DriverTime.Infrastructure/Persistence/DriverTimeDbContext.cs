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

    public DbSet<DriverActivity> DriverActivities => Set<DriverActivity>();

    public DbSet<VehicleUse> VehicleUses => Set<VehicleUse>();

    public DbSet<CountryEntry> CountryEntries => Set<CountryEntry>();

    public DbSet<Driver> Drivers => Set<Driver>();

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

            entity.Property(x => x.FileName)
                .HasMaxLength(500);

            entity.Property(x => x.DriverFirstName)
                .HasMaxLength(200);

            entity.Property(x => x.DriverLastName)
                .HasMaxLength(200);

            entity.HasOne(x => x.Company)
                .WithMany(x => x.DddFiles)
                .HasForeignKey(x => x.CompanyId);
        });

        modelBuilder.Entity<DriverActivity>(entity =>
        {
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<VehicleUse>(entity =>
        {
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<CountryEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.FirstName)
                .HasMaxLength(100);

            entity.Property(x => x.LastName)
                .HasMaxLength(100);

            entity.Property(x => x.CardNumber)
                .HasMaxLength(100);

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Drivers)
                .HasForeignKey(x => x.CompanyId);
        });
    }
}
