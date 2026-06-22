namespace DriverTime.Application.Reports.DTOs;

public class DriverMileageReportDto
{
    public Guid DriverId { get; set; }

    public string DriverName { get; set; } = string.Empty;

    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public int TotalDistanceKm { get; set; }

    public int VehicleUseCount { get; set; }

    public int MissingDistanceCount { get; set; }

    public List<DriverMileageReportRowDto> Rows { get; set; } = new();
}

public class DriverMileageReportRowDto
{
    public DateOnly Date { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string RegistrationNumber { get; set; } = string.Empty;

    public int? StartOdometerKm { get; set; }

    public int? EndOdometerKm { get; set; }

    public int? DistanceKm { get; set; }

    public bool HasDistanceData { get; set; }
}
