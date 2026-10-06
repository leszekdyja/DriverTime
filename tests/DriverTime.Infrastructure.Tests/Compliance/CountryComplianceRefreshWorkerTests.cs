using DriverTime.Infrastructure.BackgroundJobs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class CountryComplianceRefreshWorkerTests
{
    [DataTestMethod]
    [DataRow("MISSING_START_COUNTRY")]
    [DataRow("MISSING_END_COUNTRY")]
    [DataRow("INVALID_COUNTRY_CODE")]
    [DataRow("INCOMPLETE_COUNTRY_DATA")]
    public void IsCountryComplianceCode_WithCountryRuleCode_ReturnsTrue(string code)
    {
        Assert.IsTrue(CountryComplianceRefreshWorker.IsCountryComplianceCode(code));
    }

    [DataTestMethod]
    [DataRow("DAILY_DRIVING_LIMIT")]
    [DataRow("")]
    [DataRow(null)]
    public void IsCountryComplianceCode_WithOtherCode_ReturnsFalse(string? code)
    {
        Assert.IsFalse(CountryComplianceRefreshWorker.IsCountryComplianceCode(code));
    }
}
