namespace DriverTime.Application.DTOs;

public class DashboardDto
{
    public int DddFilesCount { get; set; }

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
}
