namespace DriverTime.Application.Interfaces;

public interface ICurrentUserService
{
    Guid UserId { get; }

    Guid CompanyId { get; }

    Guid? OperatingCompanyId => null;

    Guid DriverId { get; }

    bool IsMobileDriver { get; }

    bool IsAuthenticated { get; }
}
