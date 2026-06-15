namespace DriverTime.Application.Violations.DTOs;

public class DriverViolationDto
{
    public string DriverFirstName { get; set; } = string.Empty;

    public string DriverLastName { get; set; } = string.Empty;

    public string DriverCardNumber { get; set; } = string.Empty;

    public string ViolationType { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;
}
