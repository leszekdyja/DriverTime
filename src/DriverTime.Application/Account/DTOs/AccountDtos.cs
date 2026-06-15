using System.ComponentModel.DataAnnotations;

namespace DriverTime.Application.Account.DTOs;

public class AccountProfileDto
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;
}

public class UpdateAccountProfileDto
{
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    [Required]
    [MaxLength(200)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(200)]
    public string NewPassword { get; set; } = string.Empty;
}
