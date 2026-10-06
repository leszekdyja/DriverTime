using DriverTime.Application.Compliance;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.BackgroundJobs;

/// <summary>
/// Re-evaluates persisted country-entry warnings after a deployment. Country-place
/// records overlap between consecutive card downloads, so a rule correction must
/// also be applied to results that were saved before the correction was deployed.
/// </summary>
public sealed class CountryComplianceRefreshWorker : BackgroundService
{
    private static readonly string[] CountryComplianceCodes =
    [
        "MISSING_START_COUNTRY",
        "MISSING_END_COUNTRY",
        "INVALID_COUNTRY_CODE",
        "INCOMPLETE_COUNTRY_DATA"
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CountryComplianceRefreshWorker> _logger;

    public CountryComplianceRefreshWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<CountryComplianceRefreshWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<RefreshCandidate> candidates;

        try
        {
            candidates = await GetCandidatesAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load country-compliance refresh candidates.");
            return;
        }

        _logger.LogInformation(
            "Country-compliance startup refresh started. Drivers={DriverCount}.",
            candidates.Count);

        var refreshedCount = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var evaluationService = scope.ServiceProvider
                    .GetRequiredService<IComplianceEvaluationService>();

                await evaluationService.EvaluateForDriverAsync(
                    candidate.CompanyId,
                    candidate.DriverId,
                    stoppingToken);
                refreshedCount++;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Country-compliance startup refresh failed for driver {DriverId} in company {CompanyId}.",
                    candidate.DriverId,
                    candidate.CompanyId);
            }
        }

        _logger.LogInformation(
            "Country-compliance startup refresh finished. Candidates={CandidateCount}, Refreshed={RefreshedCount}.",
            candidates.Count,
            refreshedCount);
    }

    private async Task<IReadOnlyList<RefreshCandidate>> GetCandidatesAsync(
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DriverTimeDbContext>();

        return await dbContext.Violations
            .AsNoTracking()
            .Where(violation =>
                CountryComplianceCodes.Contains(violation.RegulationReference) &&
                violation.Driver != null)
            .Select(violation => new RefreshCandidate(
                violation.Driver!.CompanyId,
                violation.DriverId))
            .Distinct()
            .OrderBy(candidate => candidate.CompanyId)
            .ThenBy(candidate => candidate.DriverId)
            .ToListAsync(cancellationToken);
    }

    internal static bool IsCountryComplianceCode(string? code) =>
        code is not null && CountryComplianceCodes.Contains(code, StringComparer.Ordinal);

    private sealed record RefreshCandidate(Guid CompanyId, Guid DriverId);
}
