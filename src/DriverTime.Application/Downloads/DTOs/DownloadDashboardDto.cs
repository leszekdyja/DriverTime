namespace DriverTime.Application.Downloads.DTOs;

public class DownloadDashboardDto
{
    public int OverdueDrivers { get; set; }

    public int WarningDrivers { get; set; }

    public int OverdueVehicles { get; set; }

    public int WarningVehicles { get; set; }

    public List<DriverDownloadDto> NextDriversDue { get; set; } = new();

    public List<VehicleDownloadDto> NextVehiclesDue { get; set; } = new();
}
