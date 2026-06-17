using DriverTime.Application.Compliance;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DriverTime.Infrastructure.BackgroundJobs;

public class ComplianceSchedulerWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ComplianceSchedulerWorker> _logger;
    private readonly ComplianceSchedulerOptions _options;

    public ComplianceSchedulerWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ComplianceSchedulerOptions> options,
        ILogger<ComplianceSchedulerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Compliance scheduler worker is disabled.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunComplianceCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Compliance scheduler worker failed during cycle.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RunComplianceCycleAsync(CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTime.UtcNow;
        var maxDrivers = Math.Max(1, _options.MaxDriversPerRun);
        var drivers = await GetDriverCandidatesAsync(maxDrivers, cancellationToken);

        _logger.LogInformation(
            "Compliance scheduler cycle started. Drivers={DriverCount}, MaxDrivers={MaxDrivers}, StartedAtUtc={StartedAtUtc:o}.",
            drivers.Count,
            maxDrivers,
            startedAtUtc);

        var savedRunsCount = 0;

        foreach (var driver in drivers)
        {
            try
            {
                var saved = await RunForDriverAsync(
                    driver.CompanyId,
                    driver.DriverId,
                    cancellationToken);

                if (saved)
                {
                    savedRunsCount++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Compliance scheduler failed for driver {DriverId} in company {CompanyId}.",
                    driver.DriverId,
                    driver.CompanyId);
            }
        }

        _logger.LogInformation(
            "Compliance scheduler cycle finished. Drivers={DriverCount}, SavedRuns={SavedRunsCount}, StartedAtUtc={StartedAtUtc:o}, FinishedAtUtc={FinishedAtUtc:o}.",
            drivers.Count,
            savedRunsCount,
            startedAtUtc,
            DateTime.UtcNow);
    }

    private async Task<IReadOnlyList<DriverCandidate>> GetDriverCandidatesAsync(
        int maxDrivers,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DriverTimeDbContext>();

        return await dbContext.Drivers
            .AsNoTracking()
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Take(maxDrivers)
            .Select(x => new DriverCandidate(x.CompanyId, x.Id))
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> RunForDriverAsync(
        Guid companyId,
        Guid driverId,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var complianceEngine = scope.ServiceProvider
            .GetRequiredService<IComplianceEngineService>();
        var runHistoryService = scope.ServiceProvider
            .GetRequiredService<IComplianceRunHistoryService>();

        var preview = await complianceEngine.PreviewForDriverAsync(
            companyId,
            driverId,
            cancellationToken);

        if (preview is null)
        {
            _logger.LogWarning(
                "Compliance scheduler skipped driver {DriverId} in company {CompanyId} because preview returned null.",
                driverId,
                companyId);

            return false;
        }

        await runHistoryService.SaveRunAsync(
            companyId,
            driverId,
            preview,
            "Scheduler",
            cancellationToken);

        return true;
    }

    private sealed record DriverCandidate(Guid CompanyId, Guid DriverId);
}
