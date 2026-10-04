using DriverTime.Application.Companies.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Companies;

[TestClass]
public class CompanySettingsServiceTests
{
    [TestMethod]
    public async Task GetAsync_ForRestrictedAccount_ReturnsOperatingCompanyData()
    {
        var tenantId = Guid.NewGuid();
        var operatingCompanyId = Guid.NewGuid();
        await using var db = CreateDbContext();
        db.Companies.Add(new Company { Id = tenantId, Name = "Konto główne", VatNumber = "MAIN" });
        db.OperatingCompanies.Add(new OperatingCompany
        {
            Id = operatingCompanyId,
            CompanyId = tenantId,
            Name = "Firma lokalna",
            TaxNumber = "LOCAL",
            Address = "Adres firmy",
            Email = "firma@example.pl",
            Phone = "123"
        });
        await db.SaveChangesAsync();

        var result = await new CompanySettingsService(
            db, new RestrictedCurrentUser(tenantId, operatingCompanyId)).GetAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual("Firma lokalna", result.Name);
        Assert.AreEqual("LOCAL", result.VatNumber);
        Assert.AreEqual("firma@example.pl", result.Email);
    }

    [TestMethod]
    public async Task UpdateAsync_ForRestrictedAccount_DoesNotModifyMainCompany()
    {
        var tenantId = Guid.NewGuid();
        var operatingCompanyId = Guid.NewGuid();
        await using var db = CreateDbContext();
        db.Companies.Add(new Company { Id = tenantId, Name = "Konto główne" });
        db.OperatingCompanies.Add(new OperatingCompany
        {
            Id = operatingCompanyId,
            CompanyId = tenantId,
            Name = "Firma lokalna"
        });
        await db.SaveChangesAsync();

        await new CompanySettingsService(db, new RestrictedCurrentUser(tenantId, operatingCompanyId))
            .UpdateAsync(new UpdateCompanySettingsDto { Name = "Nowa nazwa", VatNumber = "999" });

        Assert.AreEqual("Konto główne", (await db.Companies.FindAsync(tenantId))!.Name);
        var operatingCompany = (await db.OperatingCompanies.FindAsync(operatingCompanyId))!;
        Assert.AreEqual("Nowa nazwa", operatingCompany.Name);
        Assert.AreEqual("999", operatingCompany.TaxNumber);
    }

    private static DriverTimeDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class RestrictedCurrentUser(Guid companyId, Guid operatingCompanyId) : ICurrentUserService
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = companyId;
        public Guid? OperatingCompanyId { get; } = operatingCompanyId;
        public Guid DriverId => Guid.Empty;
        public bool IsMobileDriver => false;
        public bool IsAuthenticated => true;
    }
}
