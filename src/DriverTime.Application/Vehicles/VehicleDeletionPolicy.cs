using DriverTime.Domain.Entities;

namespace DriverTime.Application.Vehicles;

public static class VehicleDeletionPolicy
{
    public static bool CanDelete(Vehicle? vehicle, Guid currentCompanyId) =>
        vehicle is not null
        && currentCompanyId != Guid.Empty
        && vehicle.CompanyId == currentCompanyId;
}
