using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Drivers;

[TestClass]
public class DriverActivitySelectionTests
{
    [TestMethod]
    public async Task CompanyAccountSelectingDriverDoesNotReceiveColleaguesActivities()
    {
        await using var fixture = new Fixture();
        var result = await fixture.Service(fixture.Scope).GetActivitiesAsync(null, null, "JEMIOLO", fixture.Selected);
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.All(x => x.DriverCardNumber == "JEMIOLO"));
    }

    [TestMethod]
    public async Task CompanyAccountSelectingCardDoesNotReceiveColleaguesActivities()
    {
        await using var fixture = new Fixture();
        var result = await fixture.Service(fixture.Scope).GetActivitiesAsync(null, null, "JEMIOLO");
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.All(x => x.DriverCardNumber == "JEMIOLO"));
    }

    [TestMethod]
    public async Task MainAccountCompanyFilterAndDriverFilterAreAppliedTogether()
    {
        await using var fixture = new Fixture();
        var result = await fixture.Service(null).GetActivitiesAsync(null, null, null, fixture.Selected, fixture.Scope);
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.All(x => x.DriverCardNumber == "JEMIOLO"));
    }

    [TestMethod]
    public async Task SelectedDriverListAlsoAppliesWithoutCompanyFilter()
    {
        await using var fixture = new Fixture();
        var result = await fixture.Service(null).GetActivitiesAsync(null, null, null, driverIds: new[] {fixture.Selected});
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.All(x => x.DriverCardNumber == "JEMIOLO"));
    }

    [TestMethod]
    public async Task WholeCompanyReportStillIncludesAllItsDriversButNoOtherCompany()
    {
        await using var fixture = new Fixture();
        var result = await fixture.Service(fixture.Scope).GetActivitiesAsync(null, null, null);
        Assert.AreEqual(4, result.Count);
        CollectionAssert.AreEquivalent(new[] {"JEMIOLO", "DYJA"}, result.Select(x => x.DriverCardNumber).Distinct().ToArray());
    }

    [TestMethod]
    public async Task SelectingDriverFromAnotherOperatingCompanyDoesNotBroadenAccess()
    {
        await using var fixture = new Fixture();
        Assert.AreEqual(0, (await fixture.Service(fixture.Scope).GetActivitiesAsync(null, null, null, fixture.OtherScopeDriver)).Count);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly DriverTimeDbContext db = new(new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        private readonly Guid company = Guid.NewGuid();
        public Guid Scope {get;} = Guid.NewGuid();
        public Guid Selected {get;}
        public Guid OtherScopeDriver {get;}

        public Fixture()
        {
            Selected = AddDriver(company, Scope, "JEMIOLO");
            AddDriver(company, Scope, "DYJA");
            OtherScopeDriver = AddDriver(company, Guid.NewGuid(), "OTHER_SCOPE");
            AddDriver(Guid.NewGuid(), Scope, "OTHER_TENANT");
            db.SaveChanges();
        }

        private Guid AddDriver(Guid tenant, Guid scope, string card)
        {
            var driver = new Driver {Id = Guid.NewGuid(), CompanyId = tenant, OperatingCompanyId = scope, CardNumber = card};
            var file = new DddFile {Id = Guid.NewGuid(), CompanyId = tenant, DriverId = driver.Id, Driver = driver, DriverCardNumber = card};
            foreach (var day in new[] {23, 24})
                db.DriverActivities.Add(new DriverActivity {Id = Guid.NewGuid(), DddFile = file, DddFileId = file.Id,
                    ActivityType = "DRIVING", StartUtc = new DateTime(2026, 10, day, 8, 0, 0, DateTimeKind.Utc),
                    EndUtc = new DateTime(2026, 10, day, 10, 0, 0, DateTimeKind.Utc)});
            return driver.Id;
        }

        public DriverActivityService Service(Guid? scope) => new(db, new User(company, scope));
        public ValueTask DisposeAsync() => db.DisposeAsync();
    }

    private sealed class User(Guid company, Guid? scope) : ICurrentUserService
    {
        public Guid UserId => Guid.Empty; public Guid CompanyId => company; public Guid? OperatingCompanyId => scope;
        public Guid DriverId => Guid.Empty; public bool IsMobileDriver => false; public bool IsAuthenticated => true;
    }
}
