using DriverTime.Application.Authentication;
using DriverTime.Application.Authentication.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Authentication;

public class AuthService : IAuthService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUserService _currentUser;

    public AuthService(
        DriverTimeDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _currentUser = currentUser;
    }

    public async Task<AuthResponseDto?> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var userRecord = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (userRecord is null)
        {
            return null;
        }

        var companyExists = await _dbContext.Companies
            .AnyAsync(x => x.Id == userRecord.CompanyId, cancellationToken);
        var roleExists = await _dbContext.Roles
            .AnyAsync(x => x.Id == userRecord.RoleId, cancellationToken);
        if (!companyExists || !roleExists)
        {
            return null;
        }

        var user = await UserQuery()
            .FirstOrDefaultAsync(x => x.Id == userRecord.Id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var passwordVerified = _passwordHasher.Verify(request.Password, user.PasswordHash);
        if (!user.Active || !passwordVerified)
        {
            return null;
        }

        return _tokenService.CreateToken(user);
    }

    public async Task<AuthResponseDto> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password)
            || request.Password.Length < 8)
        {
            throw new ArgumentException("Company, email and password with at least 8 characters are required.");
        }

        var email = NormalizeEmail(request.Email);

        if (await _dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var adminRole = await _dbContext.Roles
            .SingleAsync(x => x.Name == RoleNames.Admin, cancellationToken);
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = request.CompanyName.Trim()
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Company = company,
            Role = adminRole,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password)
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return _tokenService.CreateToken(user);
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return null;
        }

        var user = await UserQuery()
            .FirstOrDefaultAsync(x => x.Id == _currentUser.UserId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        return new CurrentUserDto
        {
            Id = user.Id,
            CompanyId = user.CompanyId,
            CompanyName = user.Company?.Name ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role.Name
        };
    }

    private IQueryable<User> UserQuery()
    {
        return _dbContext.Users
            .Include(x => x.Company)
            .Include(x => x.Role);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
