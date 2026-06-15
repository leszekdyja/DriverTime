namespace DriverTime.Application.Drivers.DTOs;

public class DriverDto
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string CardNumber { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}