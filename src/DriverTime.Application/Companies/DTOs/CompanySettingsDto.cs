using System.ComponentModel.DataAnnotations;

namespace DriverTime.Application.Companies.DTOs;

public class CompanySettingsDto
{
    public string Name { get; set; } = string.Empty;

    public string VatNumber { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;
}

public class UpdateCompanySettingsDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? VatNumber { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [EmailAddress]
    [MaxLength(320)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }
}
