using DriverTime.Application.Companies.Services;
using DriverTime.Application.CardReader;
using DriverTime.Application.Compliance;
using DriverTime.Application.Downloads;
using DriverTime.Application.Interfaces;
using DriverTime.Infrastructure.BackgroundJobs;
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

        services.Configure<ComplianceSchedulerOptions>(
            configuration.GetSection("ComplianceScheduler"));

        services.AddScoped<ICompanyService, DriverTime.Application.Companies.Services.CompanyService>();

        services.AddScoped<IDddParserGateway, DddParserGateway>();

        services.AddScoped<IDddFileService, DddFileService>();

        services.AddScoped<IDddImportMonitoringService, DddImportMonitoringService>();

        services.AddScoped<ICompanySettingsService, CompanySettingsService>();

        services.AddScoped<IAccountService, AccountService>();

        services.AddScoped<IDriverService, DriverService>();

        services.AddScoped<IDashboardService, DashboardService>();

        services.AddScoped<IDownloadScheduleService, DownloadScheduleService>();

        services.AddScoped<ICardReadSessionService, CardReadSessionService>();

        services.AddScoped<IDriverActivityService, DriverActivityService>();

        services.AddScoped<IDriverActivityCalendarService, DriverActivityCalendarService>();

        services.AddScoped<IDriverViolationService, DriverViolationService>();

        services.AddScoped<IViolationDetectionService, ViolationDetectionService>();

        services.AddScoped<IViolationQueryService, ViolationQueryService>();

        services.AddScoped<ITimelineBuilderService, TimelineBuilderService>();
        services.AddScoped<IComplianceEngineService, ComplianceEngineService>();
        services.AddScoped<IComplianceEvaluationService, ComplianceEvaluationService>();
        services.AddScoped<IComplianceRunHistoryService, ComplianceRunHistoryService>();
        services.AddScoped<IComplianceRule, DailyDrivingLimitRule>();
        services.AddScoped<IComplianceRule, ContinuousDrivingBreakRule>();
        services.AddScoped<IComplianceRule, DailyRestViolationRule>();
        services.AddScoped<IComplianceRule, ReducedDailyRestCounterRule>();
        services.AddScoped<IComplianceRule, WeeklyDrivingLimitRule>();
        services.AddScoped<IComplianceRule, BiWeeklyDrivingLimitRule>();
        services.AddScoped<IComplianceRule, RegularWeeklyRestRule>();
        services.AddScoped<IComplianceRule, ReducedWeeklyRestRule>();
        services.AddScoped<IComplianceRule, ReducedWeeklyRestCompensationRule>();
        services.AddScoped<IComplianceRule, SixTwentyFourHourPeriodsRule>();
        services.AddScoped<ICountryEntryComplianceRule, CountryEntryCompletenessRule>();

        services.AddScoped<IDriverReportExportService, DriverReportExportService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<DatabaseSeeder>();
        services.AddHostedService<ComplianceSchedulerWorker>();

        return services;
    }
}
