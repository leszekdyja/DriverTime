using DriverTime.Application.Authentication.DTOs;
using DriverTime.Domain.Entities;

namespace DriverTime.Application.Interfaces;

public interface ITokenService
{
    AuthResponseDto CreateToken(User user);
}
