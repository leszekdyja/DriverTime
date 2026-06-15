using DriverTime.Application.Authentication.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);

    Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
