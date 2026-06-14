using DriverTime.Application.Interfaces;
using DriverTime.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DriverTime.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDddImportService, DddImportService>();

        return services;
    }
}