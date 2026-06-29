using DriverTime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<DddFile> DddFiles => Set<DddFile>();

    public DbSet<DriverActivity> DriverActivities => Set<DriverActivity>();

    public DbSet<VehicleUse> VehicleUses => Set<VehicleUse>();

    public DbSet<CountryEntry> CountryEntries => Set<CountryEntry>();

    public DbSet<PlanningDuty> PlanningDuties => Set<PlanningDuty>();

    public DbSet<PlanningDutyLine> PlanningDutyLines => Set<PlanningDutyLine>();

    public DbSet<PlanningDutyStop> PlanningDutyStops => Set<PlanningDutyStop>();

    public DbSet<PlanningSchedule> PlanningSchedules => Set<PlanningSchedule>();

    public DbSet<PlanningAssignment> PlanningAssignments => Set<PlanningAssignment>();
}

