namespace DriverTime.Application.DTOs;

public class DashboardDto
{
    public int DddFilesCount { get; set; }

    public int DriverActivitiesCount { get; set; }

    public int CountryEntriesCount { get; set; }

    public int VehicleUsesCount { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
}