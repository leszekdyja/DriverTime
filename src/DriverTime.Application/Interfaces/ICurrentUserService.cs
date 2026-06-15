namespace DriverTime.Application.Interfaces;

public interface ICurrentUserService
{
    Guid UserId { get; }

    Guid CompanyId { get; }

    bool IsAuthenticated { get; }
}
