using DriverTime.Application.Account.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class AccountService : IAccountService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;

    public AccountService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
    }

    public async Task<AccountProfileDto?> GetProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserQuery()
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? null : MapProfile(user);
    }

    public async Task<AccountProfileDto?> UpdateProfileAsync(
        UpdateAccountProfileDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserQuery()
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var email = NormalizeEmail(request.Email);
        var emailInUse = await _dbContext.Users.AnyAsync(
            x => x.Id != user.Id && x.Email == email,
            cancellationToken);

        if (emailInUse)
        {
            throw new InvalidOperationException("Uzytkownik z tym adresem e-mail juz istnieje.");
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = email;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapProfile(user);
    }

    public async Task<bool> ChangePasswordAsync(
        ChangePasswordDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(
            x => x.Id == _currentUser.UserId && x.CompanyId == _currentUser.CompanyId,
            cancellationToken);

        if (user is null)
        {
            return false;
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Aktualne haslo jest nieprawidlowe.");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private IQueryable<User> GetCurrentUserQuery()
    {
        return _dbContext.Users
            .Include(x => x.Company)
            .Include(x => x.Role)
            .Where(x => x.Id == _currentUser.UserId && x.CompanyId == _currentUser.CompanyId);
    }

    private static AccountProfileDto MapProfile(User user)
    {
        return new AccountProfileDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role.Name,
            CompanyName = user.Company?.Name ?? string.Empty
        };
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
