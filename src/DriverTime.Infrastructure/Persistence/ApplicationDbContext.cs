using DriverTime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<DddFile> DddFiles => Set<DddFile>();

    public DbSet<DriverActivity> DriverActivities => Set<DriverActivity>();

    public DbSet<VehicleUse> VehicleUses => Set<VehicleUse>();

    public DbSet<CountryEntry> CountryEntries => Set<CountryEntry>();
}