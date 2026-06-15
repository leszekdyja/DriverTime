using DriverTime.Application.Account.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IAccountService
{
    Task<AccountProfileDto?> GetProfileAsync(CancellationToken cancellationToken = default);

    Task<AccountProfileDto?> UpdateProfileAsync(
        UpdateAccountProfileDto request,
        CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(
        ChangePasswordDto request,
        CancellationToken cancellationToken = default);
}
