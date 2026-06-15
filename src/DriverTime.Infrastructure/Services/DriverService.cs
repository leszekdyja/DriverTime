using DriverTime.Application.Drivers.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DriverService : IDriverService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public DriverService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<List<DriverDto>> GetAllAsync()
    {
        return await _dbContext.Drivers
            .Where(x => x.CompanyId == _currentUser.CompanyId)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Select(x => new DriverDto
            {
                Id = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName,
                CardNumber = x.CardNumber,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync();
    }

    public async Task<DriverDto?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Drivers
            .Where(x => x.Id == id && x.CompanyId == _currentUser.CompanyId)
            .Select(x => new DriverDto
            {
                Id = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName,
                CardNumber = x.CardNumber,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .FirstOrDefaultAsync();
    }

    public async Task<DriverDto> CreateAsync(CreateDriverDto dto)
    {
        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            CompanyId = _currentUser.CompanyId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            CardNumber = dto.CardNumber,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Drivers.Add(driver);

        await _dbContext.SaveChangesAsync();

        return new DriverDto
        {
            Id = driver.Id,
            FirstName = driver.FirstName,
            LastName = driver.LastName,
            CardNumber = driver.CardNumber,
            CreatedAtUtc = driver.CreatedAtUtc
        };
    }
}
