namespace DriverTime.Application.Interfaces;

public interface ICurrentUserService
{
    Guid UserId { get; }

    Guid CompanyId { get; }

    Guid DriverId { get; }

    bool IsMobileDriver { get; }

    bool IsAuthenticated { get; }
}
