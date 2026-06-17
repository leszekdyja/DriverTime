namespace DriverTime.Application.Vehicles.DTOs;

public class VehicleAnalyticsDto
{
    public Guid VehicleId { get; set; }

    public string RegistrationNumber { get; set; } = string.Empty;

    public int TotalUses { get; set; }

    public int TotalDrivers { get; set; }

    public int TotalDddImports { get; set; }

    public DateTime? FirstUseUtc { get; set; }

    public DateTime? LastUseUtc { get; set; }

    public int TotalUsageMinutes { get; set; }

    public decimal TotalUsageHours { get; set; }

    public int ActiveDays { get; set; }

    public decimal AverageUsageMinutesPerActiveDay { get; set; }

    public int UsesLast7Days { get; set; }

    public int UsesLast30Days { get; set; }

    public List<VehicleDailyUsageDto> DailyUsageLast30Days { get; set; } = new();

    public List<VehicleDriverUsageDto> DriverUsage { get; set; } = new();
}

public class VehicleDailyUsageDto
{
    public DateOnly Date { get; set; }

    public int UsesCount { get; set; }

    public int UsageMinutes { get; set; }
}

public class VehicleDriverUsageDto
{
    public Guid DriverId { get; set; }

    public string DriverName { get; set; } = string.Empty;

    public string CardNumber { get; set; } = string.Empty;

    public int UsesCount { get; set; }

    public int UsageMinutes { get; set; }

    public DateTime FirstUseUtc { get; set; }

    public DateTime LastUseUtc { get; set; }
}
