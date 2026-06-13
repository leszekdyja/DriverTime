using DriverTime.Application.Interfaces;
using DriverTime.Infrastructure.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DriverTime.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DddParserOptions>(
            configuration.GetSection("DddParser"));

        services.AddScoped<IDddParserGateway, DddParserGateway>();

        return services;
    }
}