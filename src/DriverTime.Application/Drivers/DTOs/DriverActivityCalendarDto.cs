using DriverTime.Application.Violations.DTOs;

namespace DriverTime.Application.Drivers.DTOs;

public class DriverActivityCalendarDto
{
    public Guid DriverId { get; set; }

    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public List<DriverActivityCalendarDayDto> Days { get; set; } = new();
}

public class DriverActivityCalendarDayDto
{
    public DateOnly Date { get; set; }

    public long DrivingSeconds { get; set; }

    public long WorkSeconds { get; set; }

    public long RestSeconds { get; set; }

    public long AvailabilitySeconds { get; set; }

    public long OtherSeconds { get; set; }

    public List<DriverActivityCalendarItemDto> Activities { get; set; } = new();

    public List<DriverViolationDto> Violations { get; set; } = new();
}

public class DriverActivityCalendarItemDto
{
    public Guid Id { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string ActivityType { get; set; } = string.Empty;

    public long DurationSeconds { get; set; }
}
