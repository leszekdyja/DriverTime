namespace DriverTime.Application.Companies.DTOs;

public class OperatingCompanyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public bool Active { get; set; }
    public int DriversCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class SaveOperatingCompanyDto
{
    public string Name { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
}
