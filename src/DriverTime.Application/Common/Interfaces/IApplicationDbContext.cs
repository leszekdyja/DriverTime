using DriverTime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Company> Companies { get; }

    DbSet<User> Users { get; }

    DbSet<Driver> Drivers { get; }

    DbSet<Vehicle> Vehicles { get; }

    DbSet<DriverActivity> DriverActivities { get; }

    DbSet<Violation> Violations { get; }

    DbSet<ImportFile> ImportFiles { get; }

    DbSet<Notification> Notifications { get; }

    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
