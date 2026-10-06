namespace DriverTime.Application.CardReader;

public interface ICardReadSessionService
{
    Task<IReadOnlyList<CardReadSessionDto>> GetRecentAsync(
        Guid companyId,
        Guid? scopedUserId,
        CancellationToken cancellationToken);

    Task<CardReadSessionDto> StartAsync(
        Guid companyId,
        Guid? userId,
        StartCardReadSessionRequest request,
        CancellationToken cancellationToken);

    Task<CardReadSessionDto?> CompleteAsync(
        Guid companyId,
        Guid? scopedUserId,
        Guid id,
        CompleteCardReadSessionRequest request,
        CancellationToken cancellationToken);

    Task<CardReadSessionDto?> FailAsync(
        Guid companyId,
        Guid? scopedUserId,
        Guid id,
        FailCardReadSessionRequest request,
        CancellationToken cancellationToken);
}
