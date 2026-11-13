namespace DriverTime.Application.Drivers.DTOs;

public class DeletedDriverDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? OperatingCompanyName { get; set; }
    public DateTime DeletedAtUtc { get; set; }
    public DateTime RestoreUntilUtc { get; set; }
    public bool CanRestore { get; set; }
}
