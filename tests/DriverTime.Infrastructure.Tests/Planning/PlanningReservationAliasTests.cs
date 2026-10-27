using DriverTime.Application.Planning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class PlanningReservationAliasTests
{
    [TestMethod]
    public void LocalRezCodeMatchesExistingReservationInsteadOfOrdinaryWork()
    {
        var result = PlanningEntryClassifier.Classify(DriverTime.Domain.Entities.PlanningAssignmentType.Duty, "REZ");
        Assert.AreEqual(PlanningEntryKind.Reservation, result.Kind);
        Assert.IsFalse(result.CountsTowardWorkMinutes);
        Assert.IsFalse(result.IsTimedWork);
        var existing = PlanningEntryClassifier.Classify(DriverTime.Domain.Entities.PlanningAssignmentType.Duty, "REZERWACJA");
        Assert.AreEqual(existing.IsRestDay, result.IsRestDay);
        Assert.AreEqual(existing.CountsAsConsecutiveWorkDay, result.CountsAsConsecutiveWorkDay);
    }
}
