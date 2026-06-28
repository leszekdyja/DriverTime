namespace DriverTime.Application.Vehicles.DTOs;

public class VehicleDetailsDto : VehicleDto
{
    public DateTime? LastActivityAtUtc { get; set; }

    public int DddImportsCount { get; set; }

    public List<VehicleUseHistoryDto> VehicleUses { get; set; } = new();

    public List<VehicleDriverDto> Drivers { get; set; } = new();

    public List<VehicleActivityDto> Activities { get; set; } = new();
}

public class VehicleUseHistoryDto
{
    public Guid Id { get; set; }

    public Guid DddFileId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }

    public Guid? DriverId { get; set; }

    public string DriverName { get; set; } = string.Empty;

    public string RegistrationNumber { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }
}

public class VehicleDriverDto
{
    public Guid DriverId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string CardNumber { get; set; } = string.Empty;

    public DateTime FirstUsedAtUtc { get; set; }

    public DateTime LastUsedAtUtc { get; set; }

    public int UsageCount { get; set; }
}

public class VehicleActivityDto
{
    public Guid Id { get; set; }

    public Guid DddFileId { get; set; }

    public Guid? DriverId { get; set; }

    public string? DriverName { get; set; }

    public string ActivityType { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }
}
