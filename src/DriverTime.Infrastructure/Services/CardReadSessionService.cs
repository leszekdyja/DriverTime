using DriverTime.Application.CardReader;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class CardReadSessionService : ICardReadSessionService
{
    private const string StartedStatus = "Started";
    private const string CompletedStatus = "Completed";
    private const string FailedStatus = "Failed";

    private readonly DriverTimeDbContext _dbContext;

    public CardReadSessionService(DriverTimeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CardReadSessionDto>> GetRecentAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.CardReadSessions
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(50)
            .Select(x => new CardReadSessionDto
            {
                Id = x.Id,
                CompanyId = x.CompanyId,
                UserId = x.UserId,
                Status = x.Status,
                ReaderName = x.ReaderName,
                DriverCardNumber = x.DriverCardNumber,
                DddFileId = x.DddFileId,
                ErrorMessage = x.ErrorMessage,
                Notes = x.Notes,
                StartedAtUtc = x.StartedAtUtc,
                CompletedAtUtc = x.CompletedAtUtc,
                FailedAtUtc = x.FailedAtUtc,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CardReadSessionDto> StartAsync(
        Guid companyId,
        Guid? userId,
        StartCardReadSessionRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var session = new CardReadSession
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId == Guid.Empty ? null : userId,
            Status = StartedStatus,
            ReaderName = Normalize(request.ReaderName),
            Notes = Normalize(request.Notes),
            StartedAtUtc = now,
            CreatedAtUtc = now
        };

        _dbContext.CardReadSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(session);
    }

    public async Task<CardReadSessionDto?> CompleteAsync(
        Guid companyId,
        Guid id,
        CompleteCardReadSessionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await GetSessionAsync(companyId, id, cancellationToken);
        if (session is null)
        {
            return null;
        }

        session.Status = CompletedStatus;
        session.DriverCardNumber = Normalize(request.DriverCardNumber);
        session.DddFileId = request.DddFileId;
        session.Notes = Normalize(request.Notes);
        session.ErrorMessage = string.Empty;
        session.CompletedAtUtc = DateTime.UtcNow;
        session.FailedAtUtc = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(session);
    }

    public async Task<CardReadSessionDto?> FailAsync(
        Guid companyId,
        Guid id,
        FailCardReadSessionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await GetSessionAsync(companyId, id, cancellationToken);
        if (session is null)
        {
            return null;
        }

        session.Status = FailedStatus;
        session.ErrorMessage = Normalize(request.ErrorMessage);
        session.Notes = Normalize(request.Notes);
        session.FailedAtUtc = DateTime.UtcNow;
        session.CompletedAtUtc = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(session);
    }

    private Task<CardReadSession?> GetSessionAsync(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken)
    {
        return _dbContext.CardReadSessions
            .Where(x => x.CompanyId == companyId && x.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static CardReadSessionDto Map(CardReadSession session)
    {
        return new CardReadSessionDto
        {
            Id = session.Id,
            CompanyId = session.CompanyId,
            UserId = session.UserId,
            Status = session.Status,
            ReaderName = session.ReaderName,
            DriverCardNumber = session.DriverCardNumber,
            DddFileId = session.DddFileId,
            ErrorMessage = session.ErrorMessage,
            Notes = session.Notes,
            StartedAtUtc = session.StartedAtUtc,
            CompletedAtUtc = session.CompletedAtUtc,
            FailedAtUtc = session.FailedAtUtc,
            CreatedAtUtc = session.CreatedAtUtc
        };
    }
}
