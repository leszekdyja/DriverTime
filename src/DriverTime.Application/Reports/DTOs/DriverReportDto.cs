namespace DriverTime.Application.Reports.DTOs;

public class DriverReportDto
{
    public string CompanyName { get; set; } = string.Empty;

    public string CompanyVatNumber { get; set; } = string.Empty;

    public string CompanyAddress { get; set; } = string.Empty;

    public string CompanyEmail { get; set; } = string.Empty;

    public string CompanyPhone { get; set; } = string.Empty;

    public Guid DriverId { get; set; }

    public string DriverFirstName { get; set; } = string.Empty;

    public string DriverLastName { get; set; } = string.Empty;

    public string DriverCardNumber { get; set; } = string.Empty;

    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public long DrivingSeconds { get; set; }

    public long WorkSeconds { get; set; }

    public long RestSeconds { get; set; }

    public long AvailabilitySeconds { get; set; }

    public int? TotalDistanceKm { get; set; }

    public List<DriverReportActivityDto> Activities { get; set; } = new();
}

public class DriverReportActivityDto
{
    public Guid DddFileId { get; set; }

    public Guid? VehicleUseId { get; set; }

    public string VehicleUseBusinessKey { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string ActivityType { get; set; } = string.Empty;

    public string VehicleRegistration { get; set; } = string.Empty;

    public long DurationSeconds { get; set; }

    public int? StartOdometerKm { get; set; }

    public int? EndOdometerKm { get; set; }

    public int? DistanceKm { get; set; }
}
