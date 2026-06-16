using DriverTime.Application.Companies.Services;
using DriverTime.Application.Compliance;
using DriverTime.Application.Interfaces;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Compliance.Rules;
using DriverTime.Infrastructure.Parsing;
using DriverTime.Infrastructure.Authentication;
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
        services.AddDbContext<DriverTimeDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection")));

        services.Configure<DddParserOptions>(
            configuration.GetSection("DddParser"));

        services.Configure<ImportRetryOptions>(
            configuration.GetSection("ImportRetry"));

        services.AddScoped<ICompanyService, DriverTime.Application.Companies.Services.CompanyService>();

        services.AddScoped<IDddParserGateway, DddParserGateway>();

        services.AddScoped<IDddFileService, DddFileService>();

        services.AddScoped<IDddImportMonitoringService, DddImportMonitoringService>();

        services.AddScoped<ICompanySettingsService, CompanySettingsService>();

        services.AddScoped<IAccountService, AccountService>();

        services.AddScoped<IDriverService, DriverService>();

        services.AddScoped<IDashboardService, DashboardService>();

        services.AddScoped<IDriverActivityService, DriverActivityService>();

        services.AddScoped<IDriverActivityCalendarService, DriverActivityCalendarService>();

        services.AddScoped<IDriverViolationService, DriverViolationService>();

        services.AddScoped<IViolationDetectionService, ViolationDetectionService>();

        services.AddScoped<IViolationQueryService, ViolationQueryService>();

        services.AddScoped<ITimelineBuilderService, TimelineBuilderService>();
        services.AddScoped<IComplianceEngineService, ComplianceEngineService>();
        services.AddScoped<IComplianceRule, DailyDrivingLimitRule>();
        services.AddScoped<IComplianceRule, WeeklyDrivingLimitRule>();
        services.AddScoped<IComplianceRule, BiWeeklyDrivingLimitRule>();

        services.AddScoped<IDriverReportExportService, DriverReportExportService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
