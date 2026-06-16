using DriverTime.Application.Interfaces;
using DriverTime.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace DriverTime.Api.Services;

public class DddImportRetryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DddImportRetryWorker> _logger;
    private readonly ImportRetryOptions _options;

    public DddImportRetryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ImportRetryOptions> options,
        ILogger<DddImportRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.IntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RetryFailedImportsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "DDD import retry worker failed during retry cycle.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RetryFailedImportsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var monitoringService = scope.ServiceProvider
            .GetRequiredService<IDddImportMonitoringService>();
        var dddFileService = scope.ServiceProvider
            .GetRequiredService<IDddFileService>();

        var candidates = await monitoringService.GetFailedRetryCandidatesAsync(
            _options.MaxRetryCount,
            take: 5,
            cancellationToken);

        foreach (var candidate in candidates)
        {
            await dddFileService.RetryImportAsync(candidate.Id, cancellationToken);
        }
    }
}
