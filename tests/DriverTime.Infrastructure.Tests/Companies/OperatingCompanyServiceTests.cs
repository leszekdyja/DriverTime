using DriverTime.Application.Companies.DTOs;
using DriverTime.Application.Authentication;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Companies;

[TestClass]
public class OperatingCompanyServiceTests
{
    [TestMethod]
    public async Task GetAllAsync_ReturnsOnlyCompaniesFromCurrentAccount()
    {
        var companyId = Guid.NewGuid();
        await using var db = CreateDbContext();
        db.OperatingCompanies.AddRange(
            CreateCompany(companyId, "Firma A"),
            CreateCompany(Guid.NewGuid(), "Firma obca"));
        await db.SaveChangesAsync();

        var result = await new OperatingCompanyService(db, new TestCurrentUserService(companyId)).GetAllAsync();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Firma A", result[0].Name);
    }

    [TestMethod]
    public async Task CreateAsync_AssignsCompanyToCurrentAccount()
    {
        var companyId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var service = new OperatingCompanyService(db, new TestCurrentUserService(companyId));

        var result = await service.CreateAsync(new CreateOperatingCompanyDto { Name = "  Przewozy A  ", TaxNumber = "123", Active = true });

        Assert.AreEqual("Przewozy A", result.Name);
        Assert.IsTrue(await db.OperatingCompanies.AnyAsync(x => x.Id == result.Id && x.CompanyId == companyId));
    }

    [TestMethod]
    public async Task UpdateAsync_DoesNotUpdateCompanyFromOtherAccount()
    {
        var other = CreateCompany(Guid.NewGuid(), "Firma obca");
        await using var db = CreateDbContext();
        db.OperatingCompanies.Add(other);
        await db.SaveChangesAsync();

        var result = await new OperatingCompanyService(db, new TestCurrentUserService(Guid.NewGuid()))
            .UpdateAsync(other.Id, new SaveOperatingCompanyDto { Name = "Zmieniona" });

        Assert.IsNull(result);
        Assert.AreEqual("Firma obca", (await db.OperatingCompanies.FindAsync(other.Id))!.Name);
    }

    [TestMethod]
    public async Task CreateAsync_RejectsDuplicateNameWithinAccount()
    {
        var companyId = Guid.NewGuid();
        await using var db = CreateDbContext();
        db.OperatingCompanies.Add(CreateCompany(companyId, "Firma A"));
        await db.SaveChangesAsync();
        var service = new OperatingCompanyService(db, new TestCurrentUserService(companyId));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateOperatingCompanyDto { Name = "firma a" }));
    }

    [TestMethod]
    public async Task CreateAsync_WithLoginAccount_CreatesRestrictedDispatcherUser()
    {
        var companyId = Guid.NewGuid();
        await using var db = CreateDbContext();
        db.Roles.Add(new Role { Id = Guid.NewGuid(), Name = RoleNames.Dispatcher });
        await db.SaveChangesAsync();
        var service = new OperatingCompanyService(db, new TestCurrentUserService(companyId));

        var result = await service.CreateAsync(new CreateOperatingCompanyDto
        {
            Name = "Firma z kontem",
            CreateLoginAccount = true,
            AccountFirstName = "Jan",
            AccountLastName = "Kowalski",
            AccountEmail = " FIRMA@EXAMPLE.PL ",
            AccountPassword = "bezpieczne123"
        });

        var user = await db.Users.Include(x => x.Role).SingleAsync();
        Assert.AreEqual(companyId, user.CompanyId);
        Assert.AreEqual(result.Id, user.OperatingCompanyId);
        Assert.AreEqual("firma@example.pl", user.Email);
        Assert.AreEqual(RoleNames.Dispatcher, user.Role.Name);
        Assert.AreNotEqual("bezpieczne123", user.PasswordHash);
        Assert.AreEqual("firma@example.pl", result.AccountEmail);
    }

    private static DriverTimeDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static OperatingCompany CreateCompany(Guid companyId, string name) => new()
    {
        Id = Guid.NewGuid(), CompanyId = companyId, Name = name, Active = true
    };

    private sealed class TestCurrentUserService(Guid companyId) : ICurrentUserService
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = companyId;
        public Guid DriverId => Guid.Empty;
        public bool IsMobileDriver => false;
        public bool IsAuthenticated => true;
    }
}
