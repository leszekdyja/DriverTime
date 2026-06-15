using DriverTime.Application.Violations.DTOs;

namespace DriverTime.Application.Drivers.DTOs;

public class DriverDetailsDto : DriverDto
{
    public int ImportsCount { get; set; }

    public DateTime? LastImportAtUtc { get; set; }

    public long DrivingSeconds { get; set; }

    public long WorkSeconds { get; set; }

    public long RestSeconds { get; set; }

    public long AvailabilitySeconds { get; set; }

    public List<DriverImportDto> RecentImports { get; set; } = new();

    public List<DriverDetailsActivityDto> RecentActivities { get; set; } = new();

    public List<DriverViolationDto> RecentViolations { get; set; } = new();

    public List<DriverVehicleDto> Vehicles { get; set; } = new();
}

public class DriverImportDto
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }

    public int ActivitiesCount { get; set; }
}

public class DriverDetailsActivityDto
{
    public Guid Id { get; set; }

    public string ActivityType { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public long DurationSeconds { get; set; }
}

public class DriverVehicleDto
{
    public string RegistrationNumber { get; set; } = string.Empty;

    public DateTime FirstUsedAtUtc { get; set; }

    public DateTime LastUsedAtUtc { get; set; }

    public int UsageCount { get; set; }
}
