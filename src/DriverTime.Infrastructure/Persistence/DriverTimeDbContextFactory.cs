using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DriverTime.Infrastructure.Persistence;

public class DriverTimeDbContextFactory : IDesignTimeDbContextFactory<DriverTimeDbContext>
{
    public DriverTimeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DriverTimeDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5433;Database=drivertime;Username=postgres;Password=postgres");

        return new DriverTimeDbContext(optionsBuilder.Options);
    }
}