namespace DriverTime.Application.Reports.DTOs;

public class DriverReportDto
{
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

    public List<DriverReportActivityDto> Activities { get; set; } = new();
}

public class DriverReportActivityDto
{
    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string ActivityType { get; set; } = string.Empty;

    public long DurationSeconds { get; set; }
}
