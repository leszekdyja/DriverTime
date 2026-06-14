using DriverTime.Application.Interfaces;
using DriverTime.Infrastructure.Parsing;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DriverTime.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection")));

        services.Configure<DddParserOptions>(
            configuration.GetSection("DddParser"));

        services.AddScoped<IDddParserGateway, DddParserGateway>();

        services.AddScoped<IDddFileService, DddFileService>();

        services.AddScoped<IDddImportService, DddImportService>();

        return services;
    }
}