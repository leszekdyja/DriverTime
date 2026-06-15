using DriverTime.Application.Companies.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DriverTime.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<ICompanyService, CompanyService>();

        return services;
    }
}