using DriverTime.Application.ImportMonitoring.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DddImportMonitoringService : IDddImportMonitoringService
{
    private const int MaxRecentItems = 100;

    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public DddImportMonitoringService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<DddImportMonitoringDto> CreateAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var companyId = await GetExistingCompanyIdAsync(cancellationToken);
        var userId = await GetExistingUserIdAsync(cancellationToken);

        var entry = new DddImportMonitoringEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            FileName = fileName,
            Status = DddImportMonitoringStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.DddImportMonitoringEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(entry);
    }

    public Task MarkProcessingAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return UpdateStatusAsync(
            id,
            DddImportMonitoringStatus.Processing,
            startedAtUtc: DateTime.UtcNow,
            finishedAtUtc: null,
            errorMessage: string.Empty,
            cancellationToken);
    }

    public Task MarkCompletedAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return UpdateStatusAsync(
            id,
            DddImportMonitoringStatus.Completed,
            startedAtUtc: null,
            finishedAtUtc: DateTime.UtcNow,
            errorMessage: string.Empty,
            cancellationToken);
    }

    public Task MarkFailedAsync(
        Guid id,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        return UpdateStatusAsync(
            id,
            DddImportMonitoringStatus.Failed,
            startedAtUtc: null,
            finishedAtUtc: DateTime.UtcNow,
            errorMessage: TruncateError(errorMessage),
            cancellationToken);
    }

    public Task SetStoredFilePathAsync(
        Guid id,
        string storedFilePath,
        CancellationToken cancellationToken = default)
    {
        return QueryCurrentCompanyForUpdate(id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    x => x.StoredFilePath,
                    storedFilePath),
                cancellationToken);
    }

    public Task MarkRetryProcessingAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return QueryCurrentCompanyForUpdate(id)
            .Where(x => x.Status == DddImportMonitoringStatus.Failed)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, DddImportMonitoringStatus.Processing)
                    .SetProperty(x => x.StartedAtUtc, now)
                    .SetProperty(x => x.FinishedAtUtc, (DateTime?)null)
                    .SetProperty(x => x.LastRetryAtUtc, now)
                    .SetProperty(x => x.RetryCount, x => x.RetryCount + 1)
                    .SetProperty(x => x.ErrorMessage, string.Empty),
                cancellationToken);
    }

    public async Task<IReadOnlyList<DddImportMonitoringDto>> GetFailedRetryCandidatesAsync(
        int maxRetryCount,
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, MaxRecentItems);

        var entries = await _dbContext.DddImportMonitoringEntries
            .AsNoTracking()
            .Where(x =>
                x.Status == DddImportMonitoringStatus.Failed &&
                x.RetryCount < maxRetryCount &&
                x.StoredFilePath != string.Empty)
            .OrderBy(x => x.FinishedAtUtc ?? x.CreatedAtUtc)
            .Take(safeTake)
            .ToListAsync(cancellationToken);

        return entries.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<DddImportMonitoringDto>> GetRecentAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, MaxRecentItems);

        var entries = await QueryCurrentCompany()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(safeTake)
            .ToListAsync(cancellationToken);

        return entries.Select(Map).ToList();
    }

    public async Task<DddImportMonitoringDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entry = await QueryCurrentCompany()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entry is null ? null : Map(entry);
    }

    private IQueryable<DddImportMonitoringEntry> QueryCurrentCompany()
    {
        var query = _dbContext.DddImportMonitoringEntries.AsNoTracking();

        if (_currentUser.IsAuthenticated && _currentUser.CompanyId != Guid.Empty)
        {
            return query.Where(x =>
                x.CompanyId == _currentUser.CompanyId ||
                x.CompanyId == null);
        }

        return query;
    }

    private async Task UpdateStatusAsync(
        Guid id,
        DddImportMonitoringStatus status,
        DateTime? startedAtUtc,
        DateTime? finishedAtUtc,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var query = QueryCurrentCompanyForUpdate(id);

        if (startedAtUtc.HasValue)
        {
            await query.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, status)
                    .SetProperty(x => x.ErrorMessage, errorMessage)
                    .SetProperty(x => x.StartedAtUtc, startedAtUtc),
                cancellationToken);

            return;
        }

        if (finishedAtUtc.HasValue)
        {
            await query.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, status)
                    .SetProperty(x => x.ErrorMessage, errorMessage)
                    .SetProperty(x => x.LastError, errorMessage)
                    .SetProperty(x => x.FinishedAtUtc, finishedAtUtc),
                cancellationToken);

            return;
        }

        await query.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.ErrorMessage, errorMessage),
            cancellationToken);
    }

    private IQueryable<DddImportMonitoringEntry> QueryCurrentCompanyForUpdate(Guid id)
    {
        var query = _dbContext.DddImportMonitoringEntries.Where(x => x.Id == id);

        if (_currentUser.IsAuthenticated && _currentUser.CompanyId != Guid.Empty)
        {
            return query.Where(x =>
                x.CompanyId == _currentUser.CompanyId ||
                x.CompanyId == null);
        }

        return query;
    }

    private async Task<Guid?> GetExistingCompanyIdAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return null;
        }

        var exists = await _dbContext.Companies
            .AsNoTracking()
            .AnyAsync(x => x.Id == _currentUser.CompanyId, cancellationToken);

        return exists ? _currentUser.CompanyId : null;
    }

    private async Task<Guid?> GetExistingUserIdAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return null;
        }

        var exists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == _currentUser.UserId, cancellationToken);

        return exists ? _currentUser.UserId : null;
    }

    private static DddImportMonitoringDto Map(DddImportMonitoringEntry entry)
    {
        return new DddImportMonitoringDto
        {
            Id = entry.Id,
            FileName = entry.FileName,
            Status = entry.Status.ToString(),
            ErrorMessage = entry.ErrorMessage,
            RetryCount = entry.RetryCount,
            LastRetryAtUtc = entry.LastRetryAtUtc,
            LastError = entry.LastError,
            StartedAtUtc = entry.StartedAtUtc,
            FinishedAtUtc = entry.FinishedAtUtc,
            CreatedAtUtc = entry.CreatedAtUtc,
            CompanyId = entry.CompanyId,
            UserId = entry.UserId
        };
    }

    private static string TruncateError(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return string.Empty;
        }

        return errorMessage.Length <= 4000
            ? errorMessage
            : errorMessage[..4000];
    }
}
