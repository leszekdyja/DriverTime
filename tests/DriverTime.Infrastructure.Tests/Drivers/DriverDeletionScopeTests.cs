using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Drivers;

[TestClass]
public class DriverDeletionScopeTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();
    private static readonly Guid DriverId = Guid.NewGuid();
    private static readonly Guid OtherDriverId = Guid.NewGuid();

    [TestMethod]
    public void DeletionScope_SameDriverAndCompany_IncludesDriverAndImportData()
    {
        var driver = Driver(DriverId, CompanyId);
        var dddFile = DddFile(DriverId, CompanyId);
        var violation = Violation(DriverId, CompanyId);
        var complianceRun = ComplianceRun(DriverId, CompanyId);

        Assert.IsTrue(DriverService.IsDriverInCompanyScope(driver, DriverId, CompanyId));
        Assert.IsTrue(DriverService.IsDddFileInDriverDeletionScope(dddFile, DriverId, CompanyId));
        Assert.IsTrue(DriverService.IsViolationInDriverDeletionScope(violation, DriverId, CompanyId));
        Assert.IsTrue(DriverService.IsComplianceRunInDriverDeletionScope(complianceRun, DriverId, CompanyId));
    }

    [TestMethod]
    public void DeletionScope_OtherDriver_IsNotDeleted()
    {
        Assert.IsFalse(DriverService.IsDriverInCompanyScope(Driver(OtherDriverId, CompanyId), DriverId, CompanyId));
        Assert.IsFalse(DriverService.IsDddFileInDriverDeletionScope(DddFile(OtherDriverId, CompanyId), DriverId, CompanyId));
        Assert.IsFalse(DriverService.IsViolationInDriverDeletionScope(Violation(OtherDriverId, CompanyId), DriverId, CompanyId));
        Assert.IsFalse(DriverService.IsComplianceRunInDriverDeletionScope(ComplianceRun(OtherDriverId, CompanyId), DriverId, CompanyId));
    }

    [TestMethod]
    public void DeletionScope_OtherCompany_IsNotDeleted()
    {
        Assert.IsFalse(DriverService.IsDriverInCompanyScope(Driver(DriverId, OtherCompanyId), DriverId, CompanyId));
        Assert.IsFalse(DriverService.IsDddFileInDriverDeletionScope(DddFile(DriverId, OtherCompanyId), DriverId, CompanyId));
        Assert.IsFalse(DriverService.IsViolationInDriverDeletionScope(Violation(DriverId, OtherCompanyId), DriverId, CompanyId));
        Assert.IsFalse(DriverService.IsComplianceRunInDriverDeletionScope(ComplianceRun(DriverId, OtherCompanyId), DriverId, CompanyId));
    }

    [TestMethod]
    public void DeletionScope_DriverOutsideCompany_ReturnsNotFoundEquivalent()
    {
        var driver = Driver(DriverId, OtherCompanyId);

        var existsInCurrentCompany = DriverService.IsDriverInCompanyScope(
            driver,
            DriverId,
            CompanyId);

        Assert.IsFalse(existsInCurrentCompany);
    }

    private static Driver Driver(Guid driverId, Guid companyId) =>
        new()
        {
            Id = driverId,
            CompanyId = companyId
        };

    private static DddFile DddFile(Guid driverId, Guid companyId) =>
        new()
        {
            Id = Guid.NewGuid(),
            DriverId = driverId,
            CompanyId = companyId
        };

    private static Violation Violation(Guid driverId, Guid companyId) =>
        new()
        {
            Id = Guid.NewGuid(),
            DriverId = driverId,
            Driver = Driver(driverId, companyId)
        };

    private static ComplianceRun ComplianceRun(Guid driverId, Guid companyId) =>
        new()
        {
            Id = Guid.NewGuid(),
            DriverId = driverId,
            CompanyId = companyId
        };
}
