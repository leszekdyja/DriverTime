using DriverTime.Application.Drivers;
using DriverTime.Application.Drivers.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Drivers;

[TestClass]
public class DriverWorkEvidenceServiceTests
{
    [TestMethod]
    public async Task CreateEntryAsync_AllowsMultipleNonOverlappingActivitiesOnOneDay()
    {
        var companyId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var driver = CreateDriver(companyId);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();
        var service = new DriverWorkEvidenceService(dbContext, new TestCurrentUserService(companyId));

        await service.CreateEntryAsync(driver.Id, CreateRequest(new TimeOnly(8, 0), new TimeOnly(10, 0)));
        await service.CreateEntryAsync(driver.Id, CreateRequest(new TimeOnly(10, 0), new TimeOnly(12, 0), DriverWorkEvidenceActivityType.OtherWork));

        var month = await service.GetMonthAsync(driver.Id, 2026, 9);

        Assert.IsNotNull(month);
        var day = month.Days.Single(x => x.Date == new DateOnly(2026, 9, 24));
        Assert.AreEqual(2, day.Entries.Count);
        Assert.AreEqual(120, day.DrivingMinutes);
        Assert.AreEqual(120, day.OtherWorkMinutes);
        Assert.AreEqual(240, day.TotalTrackedMinutes);
    }

    [TestMethod]
    public async Task CreateEntryAsync_RejectsOverlappingActivityForSameDriver()
    {
        var companyId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var driver = CreateDriver(companyId);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();
        var service = new DriverWorkEvidenceService(dbContext, new TestCurrentUserService(companyId));
        await service.CreateEntryAsync(driver.Id, CreateRequest(new TimeOnly(8, 0), new TimeOnly(10, 0)));

        var exception = await Assert.ThrowsExceptionAsync<DriverWorkEvidenceValidationException>(() =>
            service.CreateEntryAsync(driver.Id, CreateRequest(new TimeOnly(9, 30), new TimeOnly(11, 0))));

        CollectionAssert.Contains(exception.Errors.ToList(), "Wpis ewidencji nachodzi na istniejącą aktywność kierowcy.");
    }

    [TestMethod]
    public async Task GetMonthAsync_DoesNotReturnDriverFromOtherCompany()
    {
        var currentCompanyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var otherDriver = CreateDriver(otherCompanyId);
        dbContext.Drivers.Add(otherDriver);
        await dbContext.SaveChangesAsync();
        var service = new DriverWorkEvidenceService(dbContext, new TestCurrentUserService(currentCompanyId));

        var result = await service.GetMonthAsync(otherDriver.Id, 2026, 9);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CreateEntryAsync_StoresEntryEndingNextDay()
    {
        var companyId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var driver = CreateDriver(companyId);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();
        var service = new DriverWorkEvidenceService(dbContext, new TestCurrentUserService(companyId));

        var entry = await service.CreateEntryAsync(driver.Id, new DriverWorkEvidenceEntryRequestDto
        {
            Date = new DateOnly(2026, 9, 24),
            StartTime = new TimeOnly(22, 0),
            EndTime = new TimeOnly(6, 0),
            EndsNextDay = true,
            ActivityType = DriverWorkEvidenceActivityType.Driving
        });

        Assert.IsNotNull(entry);
        Assert.IsTrue(entry.EndsNextDay);
        Assert.AreEqual(480, entry.DurationMinutes);
    }

    private static DriverTimeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new DriverTimeDbContext(options);
    }

    private static Driver CreateDriver(Guid companyId)
    {
        return new Driver
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FirstName = "Jan",
            LastName = "Kowalski",
            CardNumber = Guid.NewGuid().ToString("N"),
            IncludeInPlanning = true
        };
    }

    private static DriverWorkEvidenceEntryRequestDto CreateRequest(
        TimeOnly start,
        TimeOnly end,
        DriverWorkEvidenceActivityType activityType = DriverWorkEvidenceActivityType.Driving)
    {
        return new DriverWorkEvidenceEntryRequestDto
        {
            Date = new DateOnly(2026, 9, 24),
            StartTime = start,
            EndTime = end,
            ActivityType = activityType
        };
    }

    private sealed class TestCurrentUserService(Guid companyId) : ICurrentUserService
    {
        public Guid UserId { get; } = Guid.NewGuid();

        public Guid CompanyId { get; } = companyId;

        public Guid DriverId => Guid.Empty;

        public bool IsMobileDriver => false;

        public bool IsAuthenticated => true;
    }
}
