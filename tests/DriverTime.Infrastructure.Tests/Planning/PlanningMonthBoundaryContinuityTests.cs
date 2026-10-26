using DriverTime.Application.Planning;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class PlanningMonthBoundaryContinuityTests
{
    [TestMethod]
    public void SameDutyContinuesForRemainingWeekdaysButNotNextWeek()
    {
        var driverId = Guid.NewGuid();
        var duty = new PlanningDuty { Id = Guid.NewGuid(), DutyNumber = "12" };
        var history = new[] { new PlanningAssignment { DriverId = driverId, PlanningDutyId = duty.Id,
            PlanningDuty = duty, Date = new DateOnly(2026, 11, 30) } };
        Assert.IsTrue(PlanningMonthBoundaryContinuity.GetScore(driverId, duty, new DateOnly(2026, 12, 1), history) < 0);
        Assert.IsTrue(PlanningMonthBoundaryContinuity.GetScore(driverId, duty, new DateOnly(2026, 12, 4), history) < 0);
        Assert.AreEqual(0m, PlanningMonthBoundaryContinuity.GetScore(driverId, duty, new DateOnly(2026, 12, 7), history));
    }

    [TestMethod]
    public void PreviousWeekAndNightDutyDoNotReceiveOrdinaryDutyPreference()
    {
        var driverId = Guid.NewGuid();
        var duty = new PlanningDuty { Id = Guid.NewGuid(), DutyNumber = "RN" };
        var assignment = new PlanningAssignment { DriverId = driverId, PlanningDutyId = duty.Id,
            PlanningDuty = duty, Date = new DateOnly(2026, 11, 30) };
        Assert.AreEqual(0m, PlanningMonthBoundaryContinuity.GetScore(driverId, duty, new DateOnly(2026, 12, 1), new[] { assignment }));
        duty.DutyNumber = "12";
        assignment.Date = new DateOnly(2026, 11, 27);
        Assert.AreEqual(0m, PlanningMonthBoundaryContinuity.GetScore(driverId, duty, new DateOnly(2026, 12, 1), new[] { assignment }));
    }

    [TestMethod]
    public void LatestDutyWinsInsteadOfEarlierDutyInTheSameWeek()
    {
        var driverId = Guid.NewGuid();
        var first = new PlanningDuty { Id = Guid.NewGuid(), DutyNumber = "12" };
        var last = new PlanningDuty { Id = Guid.NewGuid(), DutyNumber = "14" };
        var assignments = new[] {
            new PlanningAssignment { DriverId = driverId, PlanningDutyId = first.Id, PlanningDuty = first, Date = new DateOnly(2026, 9, 28) },
            new PlanningAssignment { DriverId = driverId, PlanningDutyId = last.Id, PlanningDuty = last, Date = new DateOnly(2026, 9, 30) }
        };
        Assert.AreEqual(0m, PlanningMonthBoundaryContinuity.GetScore(driverId, first, new DateOnly(2026, 10, 1), assignments));
        Assert.IsTrue(PlanningMonthBoundaryContinuity.GetScore(driverId, last, new DateOnly(2026, 10, 1), assignments) < 0);
    }

    [TestMethod]
    public void ContinuityRanksBeforeWorkloadButNeverOverridesIneligibility()
    {
        var evaluator = new PlanningCandidateEvaluator(null!);
        var continued = new PlanningCandidateEvaluation { DriverId = Guid.NewGuid(),
            ScoreBreakdown = new() { MonthBoundaryContinuityScore = -100000m } };
        var other = new PlanningCandidateEvaluation { DriverId = Guid.NewGuid(),
            ScoreBreakdown = new() { CalendarTargetDeficitScore = -5000m } };
        Assert.AreSame(continued, evaluator.ChooseBestCandidate(new[] { other, continued }));
        continued.RejectionReasons.Add(PlanningCandidateRejectionReason.DriverDutyForbidden);
        Assert.AreSame(other, evaluator.ChooseBestCandidate(new[] { other, continued }));
    }

    [TestMethod]
    public void StartedNightBlockContinuesAcrossMonthButRotationAppliesToNextBlock()
    {
        var companyId = Guid.NewGuid();
        var drivers = Enumerable.Range(0, 4).Select(i => new Driver { Id = Guid.NewGuid(), CompanyId = companyId, FirstName = $"D{i}" }).ToArray();
        var duty = new PlanningDuty { Id = Guid.NewGuid(), CompanyId = companyId, DutyNumber = "RN",
            StartTime = new TimeOnly(20, 0), EndTime = new TimeOnly(6, 0), WorkMinutes = 480 };
        var context = drivers.Take(2).Select(d => new PlanningAssignment {
            Id = Guid.NewGuid(), CompanyId = companyId, DriverId = d.Id, PlanningDutyId = duty.Id, PlanningDuty = duty,
            Date = new DateOnly(2026, 11, 30), StartDateTime = new DateTime(2026, 11, 30, 20, 0, 0),
            EndDateTime = new DateTime(2026, 12, 1, 6, 0, 0), AssignmentType = PlanningAssignmentType.Duty
        }).ToList();
        var schedules = new Dictionary<(int, int), PlanningSchedule> { [(2026, 12)] = new PlanningSchedule { Id = Guid.NewGuid() } };
        var planner = new PlanningTechnicalAssignmentPlanner(new PlanningCandidateEvaluator(new PlanningEligibilityChecker()),
            new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider()));
        var options = new PlanningGenerationOptions();
        var result = planner.PlanNightDuties(drivers, new[] { duty }, schedules, context, Array.Empty<PlanningDriverAvailability>(),
            new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 4), options, companyId, DateTime.UtcNow);
        Assert.AreEqual(8, result.Assignments.Count);
        CollectionAssert.AreEquivalent(drivers.Take(2).Select(x => x.Id).ToList(), result.Assignments.Select(x => x.DriverId).Distinct().ToList());
        var next = planner.PlanNightDuties(drivers, new[] { duty }, schedules, context, Array.Empty<PlanningDriverAvailability>(),
            new DateOnly(2026, 12, 6), new DateOnly(2026, 12, 11), options, companyId, DateTime.UtcNow);
        Assert.AreEqual(12, next.Assignments.Count);
        CollectionAssert.AreEquivalent(drivers.Skip(2).Select(x => x.Id).ToList(), next.Assignments.Select(x => x.DriverId).Distinct().ToList());
    }
}
