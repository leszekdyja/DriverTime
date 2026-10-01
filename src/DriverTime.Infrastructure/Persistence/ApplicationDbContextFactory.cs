using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DriverTime.Infrastructure.Persistence;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<DriverTimeDbContext>
{
    public DriverTimeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DriverTimeDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5434;Database=drivertime;Username=drivertime;Password=postgres");

        return new DriverTimeDbContext(optionsBuilder.Options);
    }
}