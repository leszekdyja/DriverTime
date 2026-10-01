namespace DriverTime.Application.DTOs;

public class DashboardDto
{
    public int DddFilesCount { get; set; }

    public int DriversCount { get; set; }

    public int VehiclesCount { get; set; }

    public int ViolationsCount { get; set; }

    public int DriverActivitiesCount { get; set; }

    public int CountryEntriesCount { get; set; }

    public int VehicleUsesCount { get; set; }

    public int OverdueDriverDownloads { get; set; }

    public int DriverDownloadsDueIn7Days { get; set; }

    public int DownloadsDueIn7Days { get; set; }

    public int DriverDownloadsDueIn14Days { get; set; }

    public int OverdueVehicleDownloads { get; set; }

    public int VehicleDownloadsDueIn7Days { get; set; }

    public int VehicleDownloadsDueIn14Days { get; set; }

    public int DriversWithHighViolations { get; set; }

    public int DriversWithMediumViolations { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public DateTime RangeStartUtc { get; set; }

    public DateTime RangeEndUtc { get; set; }

    public List<DashboardActivitySummaryDto> ActivitySummaries { get; set; } = new();

    public List<DashboardImportTrendDto> ImportTrend { get; set; } = new();

    public List<DashboardLatestImportDto> LatestImports { get; set; } = new();

    public List<DashboardViolationSummaryDto> ViolationSummaries { get; set; } = new();

    public List<DashboardLatestViolationDto> LatestViolations { get; set; } = new();
}

public class DashboardActivitySummaryDto
{
    public string ActivityType { get; set; } = string.Empty;

    public int Count { get; set; }

    public long DurationSeconds { get; set; }
}

public class DashboardImportTrendDto
{
    public DateTime DayUtc { get; set; }

    public int ImportsCount { get; set; }
}

public class DashboardLatestImportDto
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string DriverFirstName { get; set; } = string.Empty;

    public string DriverLastName { get; set; } = string.Empty;

    public string DriverCardNumber { get; set; } = string.Empty;

    public string DriverStatus { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }

    public int ActivitiesCount { get; set; }
}

public class DashboardViolationSummaryDto
{
    public string Severity { get; set; } = string.Empty;

    public int Count { get; set; }
}

public class DashboardLatestViolationDto
{
    public Guid Id { get; set; }

    public Guid DriverId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DriverFirstName { get; set; } = string.Empty;

    public string DriverLastName { get; set; } = string.Empty;

    public string DriverCardNumber { get; set; } = string.Empty;

    public string ViolationType { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public DateTime PeriodEndUtc { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;
}
