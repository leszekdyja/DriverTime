namespace DriverTime.Application.Companies.DTOs;

public class OperatingCompanyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public bool Active { get; set; }
    public int DriversCount { get; set; }
    public string? AccountEmail { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class CreateOperatingCompanyDto : SaveOperatingCompanyDto
{
    public bool CreateLoginAccount { get; set; }
    public string AccountFirstName { get; set; } = string.Empty;
    public string AccountLastName { get; set; } = string.Empty;
    public string AccountEmail { get; set; } = string.Empty;
    public string AccountPassword { get; set; } = string.Empty;
}

public class SaveOperatingCompanyDto
{
    public string Name { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
}
