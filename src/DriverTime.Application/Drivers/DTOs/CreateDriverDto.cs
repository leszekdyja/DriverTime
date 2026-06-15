namespace DriverTime.Application.Drivers.DTOs;

public class CreateDriverDto
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string CardNumber { get; set; } = string.Empty;
}