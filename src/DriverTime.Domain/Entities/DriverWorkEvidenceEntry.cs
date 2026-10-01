namespace DriverTime.Domain.Entities;

public class DriverWorkEvidenceEntry
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public Guid DriverId { get; set; }

    public Driver Driver { get; set; } = null!;

    public DateOnly Date { get; set; }

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }

    public DriverWorkEvidenceActivityType ActivityType { get; set; }

    public DriverWorkEvidenceEntrySource Source { get; set; } = DriverWorkEvidenceEntrySource.Manual;

    public string? VehicleRegistration { get; set; }

    public string? CountryCode { get; set; }

    public decimal? DistanceKm { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}

public enum DriverWorkEvidenceActivityType
{
    Driving = 0,
    OtherWork = 1,
    Availability = 2,
    BreakRest = 3,
    Vacation = 4,
    SickLeave = 5,
    DayOff = 6,
    OtherAbsence = 7
}

public enum DriverWorkEvidenceEntrySource
{
    Manual = 0,
    Ddd = 1,
    Correction = 2,
    MobileGps = 3
}
