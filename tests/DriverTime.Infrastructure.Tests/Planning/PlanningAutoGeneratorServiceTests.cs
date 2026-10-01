using DriverTime.Application.Planning;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class PlanningAutoGeneratorServiceTests
{
    private static readonly PlanningGenerationOptions DefaultOptions = new();

    [TestMethod]
    public void BuildInterval_DutyCrossingMidnight_EndsNextDay()
    {
        var duty = CreateDuty(Guid.NewGuid(), "RN", new TimeOnly(20, 20), new TimeOnly(7, 20));

        var interval = PlanningAutoGeneratorService.BuildInterval(new DateOnly(2026, 8, 2), duty);

        Assert.IsNotNull(interval);
        Assert.AreEqual(new DateTime(2026, 8, 2, 20, 20, 0), interval.Value.Start);
        Assert.AreEqual(new DateTime(2026, 8, 3, 7, 20, 0), interval.Value.End);
    }

    [TestMethod]
    public void Eligibility_PreviousAssignmentBlocksWhenRestIsShorterThanNineHours()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "2", new TimeOnly(15, 0), new TimeOnly(23, 0));
        var date = new DateOnly(2026, 8, 2);
        var previous = CreateAssignment(companyId, driver.Id, date.AddDays(-1), new DateTime(2026, 8, 1, 20, 20, 0), new DateTime(2026, 8, 2, 7, 20, 0));

        var evaluation = Evaluate(driver, duty, date, new[] { previous });

        Assert.IsFalse(evaluation.IsEligible);
        CollectionAssert.Contains(evaluation.RejectionReasons, PlanningCandidateRejectionReason.InsufficientDailyRestBefore);
        Assert.AreEqual(460, evaluation.RestBeforeMinutes);
    }

    [TestMethod]
    public void Eligibility_ExactlyNineHoursDailyRestIsAllowed()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "2", new TimeOnly(16, 20), new TimeOnly(23, 0));
        var date = new DateOnly(2026, 8, 2);
        var previous = CreateAssignment(companyId, driver.Id, date.AddDays(-1), new DateTime(2026, 8, 1, 20, 20, 0), new DateTime(2026, 8, 2, 7, 20, 0));

        var evaluation = Evaluate(driver, duty, date, new[] { previous });

        Assert.IsTrue(evaluation.IsEligible);
        Assert.AreEqual(540, evaluation.RestBeforeMinutes);
    }

    [TestMethod]
    public void Eligibility_OverlappingIntervalsCauseOverlappingAssignment()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "2", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var date = new DateOnly(2026, 8, 2);
        var existing = CreateAssignment(companyId, driver.Id, date, new DateTime(2026, 8, 2, 7, 0, 0), new DateTime(2026, 8, 2, 12, 0, 0));

        var evaluation = Evaluate(driver, duty, date, new[] { existing });

        Assert.IsFalse(evaluation.IsEligible);
        CollectionAssert.Contains(evaluation.RejectionReasons, PlanningCandidateRejectionReason.OverlappingAssignment);
    }

    [TestMethod]
    public void Eligibility_ManualAssignmentIsPreservedAsBlockingContext()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "2", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var date = new DateOnly(2026, 8, 2);
        var manual = CreateAssignment(companyId, driver.Id, date, new DateTime(2026, 8, 2, 6, 0, 0), new DateTime(2026, 8, 2, 14, 0, 0), PlanningAssignmentStatus.Manual);
        var context = new List<PlanningAssignment> { manual };

        var evaluation = Evaluate(driver, duty, date, context);

        Assert.AreEqual(1, context.Count);
        Assert.AreEqual(PlanningAssignmentStatus.Manual, context[0].Status);
        CollectionAssert.Contains(evaluation.RejectionReasons, PlanningCandidateRejectionReason.ManualAssignmentOnDate);
        CollectionAssert.Contains(evaluation.RejectionReasons, PlanningCandidateRejectionReason.OverlappingAssignment);
    }

    [TestMethod]
    public void Eligibility_ManualAssignmentAffectsDailyRest()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "2", new TimeOnly(12, 0), new TimeOnly(20, 0));
        var date = new DateOnly(2026, 8, 2);
        var manual = CreateAssignment(companyId, driver.Id, date.AddDays(-1), new DateTime(2026, 8, 1, 14, 0, 0), new DateTime(2026, 8, 2, 6, 0, 0), PlanningAssignmentStatus.Manual);

        var evaluation = Evaluate(driver, duty, date, new[] { manual });

        CollectionAssert.Contains(evaluation.RejectionReasons, PlanningCandidateRejectionReason.InsufficientDailyRestBefore);
    }

    [TestMethod]
    [DataRow(PlanningDriverAvailabilityType.Vacation, PlanningCandidateRejectionReason.Vacation)]
    [DataRow(PlanningDriverAvailabilityType.SickLeave, PlanningCandidateRejectionReason.SickLeave)]
    [DataRow(PlanningDriverAvailabilityType.DayOff, PlanningCandidateRejectionReason.DayOff)]
    [DataRow(PlanningDriverAvailabilityType.Unavailable, PlanningCandidateRejectionReason.Unavailable)]
    public void Eligibility_AvailabilityBlocksDriver(PlanningDriverAvailabilityType availabilityType, PlanningCandidateRejectionReason expectedReason)
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "2", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var date = new DateOnly(2026, 8, 2);
        var availability = CreateAvailability(companyId, driver.Id, date, date, availabilityType);

        var evaluation = Evaluate(driver, duty, date, Array.Empty<PlanningAssignment>(), new[] { availability });

        Assert.IsFalse(evaluation.IsEligible);
        CollectionAssert.Contains(evaluation.RejectionReasons, expectedReason);
    }

    [TestMethod]
    public void Eligibility_AvailabilityDateRangeBlocksEachCoveredDay()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "2", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var availability = CreateAvailability(companyId, driver.Id, new DateOnly(2026, 8, 2), new DateOnly(2026, 8, 4), PlanningDriverAvailabilityType.Unavailable);

        var evaluation = Evaluate(driver, duty, new DateOnly(2026, 8, 3), Array.Empty<PlanningAssignment>(), new[] { availability });

        Assert.IsFalse(evaluation.IsEligible);
        CollectionAssert.Contains(evaluation.RejectionReasons, PlanningCandidateRejectionReason.Unavailable);
    }

    [TestMethod]
    public void Eligibility_SeventhConsecutiveWorkDayIsBlocked()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "7", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var assignments = Enumerable.Range(1, 6)
            .Select(day => CreateAssignment(companyId, driver.Id, new DateOnly(2026, 8, day), new DateTime(2026, 8, day, 8, 0, 0), new DateTime(2026, 8, day, 16, 0, 0)))
            .ToList();

        var evaluation = Evaluate(driver, duty, new DateOnly(2026, 8, 7), assignments);

        Assert.IsFalse(evaluation.IsEligible);
        CollectionAssert.Contains(evaluation.RejectionReasons, PlanningCandidateRejectionReason.TooManyConsecutiveWorkDays);
        Assert.AreEqual(7, evaluation.ConsecutiveWorkDaysAfter);
    }

    [TestMethod]
    public void Eligibility_DayWithoutWorkResetsConsecutiveWorkCounter()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "7", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var assignments = new[] { 1, 2, 3, 5, 6 }
            .Select(day => CreateAssignment(companyId, driver.Id, new DateOnly(2026, 8, day), new DateTime(2026, 8, day, 8, 0, 0), new DateTime(2026, 8, day, 16, 0, 0)))
            .ToList();

        var evaluation = Evaluate(driver, duty, new DateOnly(2026, 8, 7), assignments);

        Assert.IsTrue(evaluation.IsEligible);
        Assert.AreEqual(3, evaluation.ConsecutiveWorkDaysAfter);
    }

    [TestMethod]
    public void WeeklyLimit_Exactly3600MinutesIsAllowed()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var candidateDuty = CreateDuty(companyId, "SAT", new TimeOnly(8, 0), new TimeOnly(18, 0), workMinutes: 600);
        var assignments = Enumerable.Range(1, 5)
            .Select(day => CreateAssignment(companyId, driver.Id, new DateOnly(2026, 8, 2 + day), new DateTime(2026, 8, 2 + day, 8, 0, 0), new DateTime(2026, 8, 2 + day, 18, 0, 0), duty: CreateDuty(companyId, $"D{day}", new TimeOnly(8, 0), new TimeOnly(18, 0), workMinutes: 600)))
            .ToList();

        var evaluation = Evaluate(driver, candidateDuty, new DateOnly(2026, 8, 8), assignments);

        Assert.IsTrue(evaluation.IsEligible);
        Assert.AreEqual(3000, evaluation.WeeklyWorkMinutesBefore);
        Assert.AreEqual(3600, evaluation.WeeklyWorkMinutesAfter);
    }

    [TestMethod]
    public void WeeklyLimit_Exceeding3600MinutesIsBlocked()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var candidateDuty = CreateDuty(companyId, "SAT", new TimeOnly(8, 0), new TimeOnly(18, 0), workMinutes: 700);
        var assignments = Enumerable.Range(1, 5)
            .Select(day => CreateAssignment(companyId, driver.Id, new DateOnly(2026, 8, 2 + day), new DateTime(2026, 8, 2 + day, 8, 0, 0), new DateTime(2026, 8, 2 + day, 18, 0, 0), duty: CreateDuty(companyId, $"D{day}", new TimeOnly(8, 0), new TimeOnly(18, 0), workMinutes: 600)))
            .ToList();

        var evaluation = Evaluate(driver, candidateDuty, new DateOnly(2026, 8, 8), assignments);

        Assert.IsFalse(evaluation.IsEligible);
        Assert.AreEqual(3700, evaluation.WeeklyWorkMinutesAfter);
        CollectionAssert.Contains(evaluation.RejectionReasons, PlanningCandidateRejectionReason.WeeklyWorkMinutesExceeded);
    }

    [TestMethod]
    public void WeeklyLimit_ManualAssignmentCountsTowardWeeklyLimit()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var candidateDuty = CreateDuty(companyId, "SAT", new TimeOnly(8, 0), new TimeOnly(18, 0), workMinutes: 700);
        var manualDuty = CreateDuty(companyId, "MAN", new TimeOnly(8, 0), new TimeOnly(18, 0), workMinutes: 3000);
        var manual = CreateAssignment(companyId, driver.Id, new DateOnly(2026, 8, 4), new DateTime(2026, 8, 4, 8, 0, 0), new DateTime(2026, 8, 4, 18, 0, 0), PlanningAssignmentStatus.Manual, duty: manualDuty);

        var evaluation = Evaluate(driver, candidateDuty, new DateOnly(2026, 8, 8), new[] { manual });

        Assert.AreEqual(3700, evaluation.WeeklyWorkMinutesAfter);
        CollectionAssert.Contains(evaluation.RejectionReasons, PlanningCandidateRejectionReason.WeeklyWorkMinutesExceeded);
    }

    [TestMethod]
    public void WeeklyLimit_PreviouslyGeneratedAssignmentCountsTowardWeeklyLimit()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var candidateDuty = CreateDuty(companyId, "SAT", new TimeOnly(8, 0), new TimeOnly(18, 0), workMinutes: 700);
        var generatedDuty = CreateDuty(companyId, "GEN", new TimeOnly(8, 0), new TimeOnly(18, 0), workMinutes: 3000);
        var generated = CreateAssignment(companyId, driver.Id, new DateOnly(2026, 8, 4), new DateTime(2026, 8, 4, 8, 0, 0), new DateTime(2026, 8, 4, 18, 0, 0), duty: generatedDuty);

        var evaluation = Evaluate(driver, candidateDuty, new DateOnly(2026, 8, 8), new[] { generated });

        Assert.AreEqual(3700, evaluation.WeeklyWorkMinutesAfter);
        CollectionAssert.Contains(evaluation.RejectionReasons, PlanningCandidateRejectionReason.WeeklyWorkMinutesExceeded);
    }

    [TestMethod]
    public void IsoWeek_MondayToSundayShareOneWeek()
    {
        var monday = PlanningWorkloadCalculator.GetIsoWeekKey(new DateOnly(2026, 8, 3));
        var sunday = PlanningWorkloadCalculator.GetIsoWeekKey(new DateOnly(2026, 8, 9));

        Assert.AreEqual(monday, sunday);
    }

    [TestMethod]
    public void IsoWeek_SundayAndNextMondayAreDifferentWeeks()
    {
        var sunday = PlanningWorkloadCalculator.GetIsoWeekKey(new DateOnly(2026, 8, 9));
        var monday = PlanningWorkloadCalculator.GetIsoWeekKey(new DateOnly(2026, 8, 10));

        Assert.AreNotEqual(sunday, monday);
    }

    [TestMethod]
    public void WorkMinutesResolver_UsesWorkMinutesBeforeTotalDuration()
    {
        var duty = CreateDuty(Guid.NewGuid(), "A", new TimeOnly(8, 0), new TimeOnly(16, 0), workMinutes: 420, totalDurationMinutes: 480);
        PlanningWorkInterval.TryCreate(new DateOnly(2026, 8, 3), duty, out var interval);

        var result = PlanningDutyWorkMinutesResolver.Resolve(duty, interval);

        Assert.AreEqual(420, result.WorkMinutes);
        Assert.AreEqual(PlanningDutyWorkMinutesSource.ExplicitWorkMinutes, result.Source);
    }

    [TestMethod]
    public void WorkMinutesResolver_FallsBackToTotalDurationWhenWorkMinutesMissing()
    {
        var duty = CreateDuty(Guid.NewGuid(), "A", new TimeOnly(8, 0), new TimeOnly(16, 0), totalDurationMinutes: 480);
        PlanningWorkInterval.TryCreate(new DateOnly(2026, 8, 3), duty, out var interval);

        var result = PlanningDutyWorkMinutesResolver.Resolve(duty, interval);

        Assert.AreEqual(480, result.WorkMinutes);
        Assert.AreEqual(PlanningDutyWorkMinutesSource.TotalDurationFallback, result.Source);
    }

    [TestMethod]
    public void WorkMinutesResolver_FallsBackToIntervalWhenDutyMinutesMissing()
    {
        var duty = CreateDuty(Guid.NewGuid(), "A", new TimeOnly(8, 0), new TimeOnly(16, 30));
        PlanningWorkInterval.TryCreate(new DateOnly(2026, 8, 3), duty, out var interval);

        var result = PlanningDutyWorkMinutesResolver.Resolve(duty, interval);

        Assert.AreEqual(510, result.WorkMinutes);
        Assert.AreEqual(PlanningDutyWorkMinutesSource.IntervalDurationFallback, result.Source);
    }

    [TestMethod]
    public void Evaluator_ChoosesCandidateWithHigherMonthlyDeficit()
    {
        var companyId = Guid.NewGuid();
        var loadedDriver = CreateDriver(companyId, "Adam", "Loaded");
        var deficitDriver = CreateDriver(companyId, "Beata", "Deficit");
        var duty = CreateDuty(companyId, "8", new TimeOnly(8, 0), new TimeOnly(16, 0), workMinutes: 480);
        var oldDuty = CreateDuty(companyId, "OLD", new TimeOnly(8, 0), new TimeOnly(16, 0), workMinutes: 1000);
        var assignment = CreateAssignment(companyId, loadedDriver.Id, new DateOnly(2026, 8, 1), new DateTime(2026, 8, 1, 8, 0, 0), new DateTime(2026, 8, 1, 16, 0, 0), duty: oldDuty);
        var evaluator = new PlanningCandidateEvaluator(new PlanningEligibilityChecker());
        var options = DefaultOptions with { TargetMonthlyWorkMinutes = 3000 };

        var evaluations = evaluator.EvaluateCandidates(new[] { loadedDriver, deficitDriver }, duty, new DateOnly(2026, 8, 3), new[] { assignment }, Array.Empty<PlanningDriverAvailability>(), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), options);
        var selected = evaluator.ChooseBestCandidate(evaluations);

        Assert.IsNotNull(selected);
        Assert.AreEqual(deficitDriver.Id, selected.DriverId);
    }

    [TestMethod]
    public void Evaluator_ChoosesLowerWeeklyLoadWhenMonthlyDeficitIsEqual()
    {
        var companyId = Guid.NewGuid();
        var sameWeekDriver = CreateDriver(companyId, "Adam", "SameWeek");
        var previousWeekDriver = CreateDriver(companyId, "Beata", "PreviousWeek");
        var duty = CreateDuty(companyId, "8", new TimeOnly(8, 0), new TimeOnly(16, 0), workMinutes: 480);
        var oldDuty = CreateDuty(companyId, "OLD", new TimeOnly(8, 0), new TimeOnly(16, 0), workMinutes: 600);
        var assignments = new[]
        {
            CreateAssignment(companyId, sameWeekDriver.Id, new DateOnly(2026, 8, 3), new DateTime(2026, 8, 3, 8, 0, 0), new DateTime(2026, 8, 3, 16, 0, 0), duty: oldDuty),
            CreateAssignment(companyId, previousWeekDriver.Id, new DateOnly(2026, 8, 1), new DateTime(2026, 8, 1, 8, 0, 0), new DateTime(2026, 8, 1, 16, 0, 0), duty: oldDuty)
        };
        var evaluator = new PlanningCandidateEvaluator(new PlanningEligibilityChecker());
        var options = DefaultOptions with { TargetMonthlyWorkMinutes = 3000 };

        var evaluations = evaluator.EvaluateCandidates(new[] { sameWeekDriver, previousWeekDriver }, duty, new DateOnly(2026, 8, 5), assignments, Array.Empty<PlanningDriverAvailability>(), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), options);
        var selected = evaluator.ChooseBestCandidate(evaluations);

        Assert.IsNotNull(selected);
        Assert.AreEqual(previousWeekDriver.Id, selected.DriverId);
    }

    [TestMethod]
    public void Evaluator_ResultIsDeterministicForIdenticalCandidates()
    {
        var companyId = Guid.NewGuid();
        var highGuidDriver = CreateDriver(companyId, "Adam", "High", Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var lowGuidDriver = CreateDriver(companyId, "Beata", "Low", Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var duty = CreateDuty(companyId, "8", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var evaluator = new PlanningCandidateEvaluator(new PlanningEligibilityChecker());

        var evaluations = evaluator.EvaluateCandidates(new[] { highGuidDriver, lowGuidDriver }, duty, new DateOnly(2026, 8, 3), Array.Empty<PlanningAssignment>(), Array.Empty<PlanningDriverAvailability>(), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), DefaultOptions);
        var selected = evaluator.ChooseBestCandidate(evaluations);

        Assert.IsNotNull(selected);
        Assert.AreEqual(lowGuidDriver.Id, selected.DriverId);
    }

    [TestMethod]
    public void MonthlyTarget_ExceededIsBlockedOnlyWhenEnforced()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "NEW", new TimeOnly(8, 0), new TimeOnly(16, 0), workMinutes: 400);
        var oldDuty = CreateDuty(companyId, "OLD", new TimeOnly(8, 0), new TimeOnly(12, 0), workMinutes: 200);
        var assignment = CreateAssignment(companyId, driver.Id, new DateOnly(2026, 8, 1), new DateTime(2026, 8, 1, 8, 0, 0), new DateTime(2026, 8, 1, 12, 0, 0), duty: oldDuty);

        var enforced = Evaluate(driver, duty, new DateOnly(2026, 8, 3), new[] { assignment }, options: DefaultOptions with { TargetMonthlyWorkMinutes = 500, EnforceMonthlyTargetMaximum = true });
        var notEnforced = Evaluate(driver, duty, new DateOnly(2026, 8, 3), new[] { assignment }, options: DefaultOptions with { TargetMonthlyWorkMinutes = 500, EnforceMonthlyTargetMaximum = false });

        CollectionAssert.Contains(enforced.RejectionReasons, PlanningCandidateRejectionReason.MonthlyWorkMinutesExceeded);
        CollectionAssert.DoesNotContain(notEnforced.RejectionReasons, PlanningCandidateRejectionReason.MonthlyWorkMinutesExceeded);
    }

    [TestMethod]
    public void MonthlyTarget_MissingTargetDoesNotBlockMonthlyMinutes()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "NEW", new TimeOnly(8, 0), new TimeOnly(16, 0), workMinutes: 4000);

        var evaluation = Evaluate(driver, duty, new DateOnly(2026, 8, 3), Array.Empty<PlanningAssignment>(), options: DefaultOptions with { TargetMonthlyWorkMinutes = null, EnforceMonthlyTargetMaximum = true });

        CollectionAssert.DoesNotContain(evaluation.RejectionReasons, PlanningCandidateRejectionReason.MonthlyWorkMinutesExceeded);
    }

    [TestMethod]
    public void DriverSummaries_ContainCorrectBeforeAndAfterMinutes()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var oldDuty = CreateDuty(companyId, "OLD", new TimeOnly(8, 0), new TimeOnly(12, 0), workMinutes: 240);
        var newDuty = CreateDuty(companyId, "NEW", new TimeOnly(13, 0), new TimeOnly(17, 0), workMinutes: 300);
        var initial = new[] { CreateAssignment(companyId, driver.Id, new DateOnly(2026, 8, 1), new DateTime(2026, 8, 1, 8, 0, 0), new DateTime(2026, 8, 1, 12, 0, 0), PlanningAssignmentStatus.Manual, duty: oldDuty) };
        var generated = new[] { CreateAssignment(companyId, driver.Id, new DateOnly(2026, 8, 2), new DateTime(2026, 8, 2, 13, 0, 0), new DateTime(2026, 8, 2, 17, 0, 0), duty: newDuty) };
        var final = initial.Concat(generated).ToList();

        var summary = PlanningAutoGeneratorService.BuildDriverSummaries(new[] { driver }, initial, final, generated, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), DefaultOptions with { TargetMonthlyWorkMinutes = 1000 }).Single();

        Assert.AreEqual(240, summary.WorkMinutesBefore);
        Assert.AreEqual(300, summary.GeneratedWorkMinutes);
        Assert.AreEqual(540, summary.WorkMinutesAfter);
        Assert.AreEqual(460, summary.MonthlyDeficitAfter);
        Assert.AreEqual(1, summary.ExistingManualAssignments);
    }

    [TestMethod]
    public void ConsecutiveWorkDays_PreviousMonthAssignmentsAreIncluded()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "NEW", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var assignments = Enumerable.Range(26, 6)
            .Select(day => CreateAssignment(companyId, driver.Id, new DateOnly(2026, 7, day), new DateTime(2026, 7, day, 8, 0, 0), new DateTime(2026, 7, day, 16, 0, 0)))
            .ToList();

        var evaluation = Evaluate(driver, duty, new DateOnly(2026, 8, 1), assignments);

        CollectionAssert.Contains(evaluation.RejectionReasons, PlanningCandidateRejectionReason.TooManyConsecutiveWorkDays);
        Assert.AreEqual(7, evaluation.ConsecutiveWorkDaysAfter);
    }

    [TestMethod]
    public void Workload_ManualEntriesAffectWorkMinutesBefore()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "NEW", new TimeOnly(8, 0), new TimeOnly(16, 0), workMinutes: 480);
        var manualDuty = CreateDuty(companyId, "MAN", new TimeOnly(8, 0), new TimeOnly(12, 0), workMinutes: 240);
        var manual = CreateAssignment(companyId, driver.Id, new DateOnly(2026, 8, 1), new DateTime(2026, 8, 1, 8, 0, 0), new DateTime(2026, 8, 1, 12, 0, 0), PlanningAssignmentStatus.Manual, duty: manualDuty);

        var evaluation = Evaluate(driver, duty, new DateOnly(2026, 8, 3), new[] { manual });

        Assert.AreEqual(240, evaluation.MonthlyWorkMinutesBefore);
        Assert.AreEqual(720, evaluation.MonthlyWorkMinutesAfter);
    }

    [TestMethod]
    public void UnassignedDuty_ReportsWeeklyWorkMinutesExceededAndPolishSummary()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "NEW", new TimeOnly(8, 0), new TimeOnly(16, 0), workMinutes: 700);
        var oldDuty = CreateDuty(companyId, "OLD", new TimeOnly(8, 0), new TimeOnly(18, 0), workMinutes: 3000);
        var assignment = CreateAssignment(companyId, driver.Id, new DateOnly(2026, 8, 4), new DateTime(2026, 8, 4, 8, 0, 0), new DateTime(2026, 8, 4, 18, 0, 0), duty: oldDuty);
        var evaluator = new PlanningCandidateEvaluator(new PlanningEligibilityChecker());

        var evaluations = evaluator.EvaluateCandidates(new[] { driver }, duty, new DateOnly(2026, 8, 8), new[] { assignment }, Array.Empty<PlanningDriverAvailability>(), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), DefaultOptions);
        var dto = PlanningCandidateEvaluationDto.FromEvaluation(evaluations.Single());
        var summary = string.Join(" ", dto.RejectionReasonDescriptions);

        Assert.IsFalse(dto.IsEligible);
        CollectionAssert.Contains(dto.RejectionReasons, PlanningCandidateRejectionReason.WeeklyWorkMinutesExceeded.ToString());
        StringAssert.Contains(summary, "tygodniowy limit pracy");
    }

    [TestMethod]
    public void Evaluator_UsesInMemoryCollectionsWithoutDatabaseQueriesPerCandidate()
    {
        var companyId = Guid.NewGuid();
        var drivers = new[] { CreateDriver(companyId, "Adam", "A"), CreateDriver(companyId, "Beata", "B") };
        var duty = CreateDuty(companyId, "NEW", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var evaluator = new PlanningCandidateEvaluator(new PlanningEligibilityChecker());

        var evaluations = evaluator.EvaluateCandidates(drivers, duty, new DateOnly(2026, 8, 3), Array.Empty<PlanningAssignment>(), Array.Empty<PlanningDriverAvailability>(), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), DefaultOptions);

        Assert.AreEqual(2, evaluations.Count);
        Assert.IsTrue(evaluations.All(x => x.IsEligible));
    }

    [TestMethod]
    public void Calendar_January2026HolidayOnWeekdayReducesTarget()
    {
        var calendar = new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider())
            .BuildMonthlyCalendar(2026, 1);

        Assert.AreEqual(22, calendar.WeekdayCount);
        Assert.AreEqual(2, calendar.PublicHolidayReductionDays);
        Assert.AreEqual(20, calendar.WorkingDays);
        Assert.AreEqual(9600, calendar.TargetWorkMinutes);
    }

    [TestMethod]
    public void Calendar_HolidayOnSaturdayReducesTargetButSundayDoesNot()
    {
        var calendar = new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider())
            .BuildMonthlyCalendar(2026, 8);

        Assert.IsTrue(calendar.Holidays.Any(x => x.Date == new DateOnly(2026, 8, 15)));
        Assert.AreEqual(1, calendar.PublicHolidayReductionDays);
        Assert.AreEqual(9600, calendar.TargetWorkMinutes);
    }

    [TestMethod]
    public void Calendar_MovableHolidaysAreCalculated()
    {
        var holidays = new PolishPublicHolidayProvider().GetHolidays(2026);

        Assert.IsTrue(holidays.Any(x => x.Date == new DateOnly(2026, 4, 6) && x.Name.Contains("Wielkanocny")));
        Assert.IsTrue(holidays.Any(x => x.Date == new DateOnly(2026, 6, 4) && x.Name.Contains("Boże Ciało")));
    }

    [TestMethod]
    public void Options_RequestTargetHasPriorityOverCalendarTarget()
    {
        var request = new PlanningAutoGenerateRequestDto
        {
            DateFrom = new DateOnly(2026, 1, 1),
            DateTo = new DateOnly(2026, 1, 31),
            TargetMonthlyWorkMinutes = 1234
        };
        var calendar = new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider()).BuildMonthlyCalendar(2026, 1);

        var options = PlanningAutoGeneratorService.ResolveMonthlyTargetOptions(
            request,
            PlanningAutoGeneratorService.BuildOptions(request),
            new[] { calendar });

        Assert.AreEqual(1234, options.TargetMonthlyWorkMinutes);
        Assert.AreEqual(PlanningMonthlyTargetWorkMinutesSource.Request, options.TargetMonthlyWorkMinutesSource);
    }

    [TestMethod]
    public void Options_CalendarTargetIsUsedWhenRequestTargetIsMissing()
    {
        var request = new PlanningAutoGenerateRequestDto
        {
            DateFrom = new DateOnly(2026, 1, 1),
            DateTo = new DateOnly(2026, 1, 31)
        };
        var calendar = new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider()).BuildMonthlyCalendar(2026, 1);

        var options = PlanningAutoGeneratorService.ResolveMonthlyTargetOptions(
            request,
            PlanningAutoGeneratorService.BuildOptions(request),
            new[] { calendar });

        Assert.AreEqual(calendar.TargetWorkMinutes, options.TargetMonthlyWorkMinutes);
        Assert.AreEqual(PlanningMonthlyTargetWorkMinutesSource.Calendar, options.TargetMonthlyWorkMinutesSource);
    }

    [TestMethod]
    public void Vacation_OnWorkingMondayCredits480MinutesToMonthlyNormOnly()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var availability = CreateAvailability(companyId, driver.Id, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 3), PlanningDriverAvailabilityType.Vacation);
        var calendarService = new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider());

        var workload = PlanningWorkloadCalculator.WorkloadSummary(
            driver.Id,
            Array.Empty<PlanningAssignment>(),
            new[] { availability },
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            DefaultOptions,
            calendarService);

        Assert.AreEqual(0, workload.RealWorkMinutes);
        Assert.AreEqual(480, workload.CreditedAbsenceMinutes);
        Assert.AreEqual(480, workload.MonthlyNormMinutes);
    }

    [TestMethod]
    public void Vacation_OnSaturdaySundayAndHolidayCreditsZeroMinutes()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var availabilities = new[]
        {
            CreateAvailability(companyId, driver.Id, new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 15), PlanningDriverAvailabilityType.Vacation),
            CreateAvailability(companyId, driver.Id, new DateOnly(2026, 8, 16), new DateOnly(2026, 8, 16), PlanningDriverAvailabilityType.Vacation),
            CreateAvailability(companyId, driver.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), PlanningDriverAvailabilityType.Vacation)
        };
        var calendarService = new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider());

        var august = PlanningWorkloadCalculator.WorkloadSummary(driver.Id, Array.Empty<PlanningAssignment>(), availabilities, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), DefaultOptions, calendarService);
        var january = PlanningWorkloadCalculator.WorkloadSummary(driver.Id, Array.Empty<PlanningAssignment>(), availabilities, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), DefaultOptions, calendarService);

        Assert.AreEqual(0, august.CreditedAbsenceMinutes);
        Assert.AreEqual(0, january.CreditedAbsenceMinutes);
    }

    [TestMethod]
    public void Classifier_SpecialCodesAreMappedWithoutMigration()
    {
        var companyId = Guid.NewGuid();

        Assert.IsTrue(PlanningEntryClassifier.Classify(CreateDuty(companyId, "RN", new TimeOnly(20, 0), new TimeOnly(6, 0))).IsRealWork);
        Assert.IsTrue(PlanningEntryClassifier.Classify(CreateDuty(companyId, "R", new TimeOnly(8, 0), new TimeOnly(16, 0))).IsRealWork);
        Assert.IsTrue(PlanningEntryClassifier.Classify(CreateDuty(companyId, "R2", new TimeOnly(14, 0), new TimeOnly(22, 0))).IsRealWork);
        Assert.IsTrue(PlanningEntryClassifier.Classify(CreateDuty(companyId, "WG", new TimeOnly(0, 0), new TimeOnly(0, 1))).IsRestDay);
        Assert.IsTrue(PlanningEntryClassifier.Classify(CreateDuty(companyId, "W", new TimeOnly(0, 0), new TimeOnly(0, 1))).IsRestDay);
        Assert.IsTrue(PlanningEntryClassifier.Classify(CreateDuty(companyId, "UW", new TimeOnly(0, 0), new TimeOnly(0, 1))).IsCreditedAbsence);
        Assert.IsFalse(PlanningEntryClassifier.Classify(CreateDuty(companyId, "UO", new TimeOnly(0, 0), new TimeOnly(0, 1))).CountsTowardMonthlyNorm);
        Assert.IsFalse(PlanningEntryClassifier.Classify(CreateDuty(companyId, "CH", new TimeOnly(0, 0), new TimeOnly(0, 1))).CountsTowardMonthlyNorm);
    }

    [TestMethod]
    public void WeeklyRest_ClassificationUsesTwentyFourThirtyFiveAndFortyFiveHours()
    {
        Assert.AreEqual(PlanningWeeklyRestKind.Insufficient, PlanningWeeklyRestValidator.ClassifyRest(1439, DefaultOptions));
        Assert.AreEqual(PlanningWeeklyRestKind.Reduced, PlanningWeeklyRestValidator.ClassifyRest(1440, DefaultOptions));
        Assert.AreEqual(PlanningWeeklyRestKind.PreferredReduced, PlanningWeeklyRestValidator.ClassifyRest(2100, DefaultOptions));
        Assert.AreEqual(PlanningWeeklyRestKind.Regular, PlanningWeeklyRestValidator.ClassifyRest(2700, DefaultOptions));
    }

    [TestMethod]
    public void WeeklyRest_CandidateBreakingExistingTwentyFourHourRestIsRejected()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var previous = CreateAssignment(companyId, driver.Id, new DateOnly(2026, 8, 1), new DateTime(2026, 8, 1, 8, 0, 0), new DateTime(2026, 8, 1, 16, 0, 0));
        var next = CreateAssignment(companyId, driver.Id, new DateOnly(2026, 8, 2), new DateTime(2026, 8, 2, 16, 0, 0), new DateTime(2026, 8, 2, 23, 0, 0));
        var duty = CreateDuty(companyId, "2", new TimeOnly(4, 0), new TimeOnly(12, 0));

        var evaluation = Evaluate(driver, duty, new DateOnly(2026, 8, 2), new[] { previous, next });

        Assert.IsFalse(evaluation.IsEligible);
        CollectionAssert.Contains(evaluation.RejectionReasons, PlanningCandidateRejectionReason.InsufficientWeeklyRest);
    }
    [TestMethod]
    public void TechnicalPlanner_NightDutyRequiredCountMatchesOldPlannerRules()
    {
        Assert.AreEqual(2, PlanningTechnicalAssignmentPlanner.NightDutyRequiredCount(new DateOnly(2026, 8, 2)));
        Assert.AreEqual(1, PlanningTechnicalAssignmentPlanner.NightDutyRequiredCount(new DateOnly(2026, 8, 8)));
        Assert.AreEqual(1, PlanningTechnicalAssignmentPlanner.NightDutyRequiredCount(new DateOnly(2026, 1, 1)));
    }

    [TestMethod]
    public void TechnicalPlanner_NightDutyIsPlannedBeforeRegularServicesInBlocks()
    {
        var companyId = Guid.NewGuid();
        var drivers = Enumerable.Range(1, 4).Select(index => CreateDriver(companyId, $"D{index}", "RN")).ToList();
        var rnDuty = CreateDuty(companyId, "RN", new TimeOnly(20, 0), new TimeOnly(6, 0), workMinutes: 480);
        var schedule = CreateSchedule(companyId, 2026, 8);
        var schedules = new Dictionary<(int Year, int Month), PlanningSchedule> { [(2026, 8)] = schedule };
        var context = new List<PlanningAssignment>();
        var planner = new PlanningTechnicalAssignmentPlanner(
            new PlanningCandidateEvaluator(new PlanningEligibilityChecker()),
            new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider()));

        var result = planner.PlanNightDuties(
            drivers,
            new[] { rnDuty },
            schedules,
            context,
            Array.Empty<PlanningDriverAvailability>(),
            new DateOnly(2026, 8, 2),
            new DateOnly(2026, 8, 8),
            DefaultOptions,
            companyId,
            DateTime.UtcNow);

        Assert.AreEqual(13, result.Assignments.Count);
        Assert.AreEqual(2, result.Assignments.Count(x => x.Date == new DateOnly(2026, 8, 2)));
        Assert.AreEqual(1, result.Assignments.Count(x => x.Date == new DateOnly(2026, 8, 8)));
        Assert.AreEqual(2, result.Assignments.Where(x => x.Date <= new DateOnly(2026, 8, 7)).Select(x => x.DriverId).Distinct().Count());
    }

    [TestMethod]
    public void TechnicalPlanner_FinalDayOffsFillEmptyCellsWithWgAndW()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var wgDuty = CreateDuty(companyId, "WG", new TimeOnly(0, 0), new TimeOnly(0, 1));
        var wDuty = CreateDuty(companyId, "W", new TimeOnly(0, 0), new TimeOnly(0, 1));
        var schedule = CreateSchedule(companyId, 2026, 8);
        var schedules = new Dictionary<(int Year, int Month), PlanningSchedule> { [(2026, 8)] = schedule };
        var context = new List<PlanningAssignment>();
        var planner = new PlanningTechnicalAssignmentPlanner(
            new PlanningCandidateEvaluator(new PlanningEligibilityChecker()),
            new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider()));

        var result = planner.PlanFinalDayOffs(
            new[] { driver },
            new[] { wgDuty, wDuty },
            schedules,
            context,
            Array.Empty<PlanningDriverAvailability>(),
            new DateOnly(2026, 8, 3),
            new DateOnly(2026, 8, 8),
            companyId,
            DateTime.UtcNow);

        Assert.AreEqual(6, result.Assignments.Count);
        Assert.AreEqual(wgDuty.Id, result.Assignments.Single(x => x.Date == new DateOnly(2026, 8, 3)).PlanningDutyId);
        Assert.AreEqual(wDuty.Id, result.Assignments.Single(x => x.Date == new DateOnly(2026, 8, 8)).PlanningDutyId);
    }

    [TestMethod]
    public void TechnicalPlanner_ReservesAreAddedAfterServicesToCloseMonthlyNormDeficit()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var reserveDuty = CreateDuty(companyId, "R", new TimeOnly(8, 0), new TimeOnly(16, 0), workMinutes: 480);
        var schedule = CreateSchedule(companyId, 2026, 8);
        var schedules = new Dictionary<(int Year, int Month), PlanningSchedule> { [(2026, 8)] = schedule };
        var context = new List<PlanningAssignment>();
        var planner = new PlanningTechnicalAssignmentPlanner(
            new PlanningCandidateEvaluator(new PlanningEligibilityChecker()),
            new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider()));

        var result = planner.PlanReserves(
            new[] { driver },
            new[] { reserveDuty },
            schedules,
            context,
            Array.Empty<PlanningDriverAvailability>(),
            Array.Empty<PlanningUnassignedDutyDto>(),
            new DateOnly(2026, 8, 3),
            new DateOnly(2026, 8, 3),
            DefaultOptions with { TargetMonthlyWorkMinutes = 480, TargetMonthlyWorkMinutesSource = PlanningMonthlyTargetWorkMinutesSource.Request },
            companyId,
            DateTime.UtcNow);

        Assert.AreEqual(1, result.Assignments.Count);
        Assert.AreEqual(reserveDuty.Id, result.Assignments.Single().PlanningDutyId);
    }
    [TestMethod]
    public void SwapPlanner_MissingDutyIsRescuedFromAutomaticWg()
    {
        var fixture = CreateSwapFixture("WG");
        var result = fixture.Planner.RescueUnassignedDuties(fixture.Drivers, fixture.Duties, fixture.Schedules, fixture.Context, Array.Empty<PlanningDriverAvailability>(), new[] { fixture.Unassigned }, fixture.Date, fixture.Date, DefaultOptions, fixture.CompanyId, DateTime.UtcNow);

        Assert.AreEqual(1, result.RescuedDutyCount);
        Assert.AreEqual(1, result.DirectSwapCount);
        Assert.AreEqual(1, result.RemovedWeeklyDayOffCount);
        Assert.IsTrue(fixture.Context.Any(x => x.DriverId == fixture.TechnicalDriver.Id && x.PlanningDutyId == fixture.MissingDuty.Id));
        Assert.IsFalse(fixture.Context.Any(x => x.PlanningDutyId == fixture.TechnicalDuty.Id));
    }

    [TestMethod]
    public void SwapPlanner_MissingDutyIsRescuedFromAutomaticW()
    {
        var fixture = CreateSwapFixture("W");
        var result = fixture.Planner.RescueUnassignedDuties(fixture.Drivers, fixture.Duties, fixture.Schedules, fixture.Context, Array.Empty<PlanningDriverAvailability>(), new[] { fixture.Unassigned }, fixture.Date, fixture.Date, DefaultOptions, fixture.CompanyId, DateTime.UtcNow);

        Assert.AreEqual(1, result.RescuedDutyCount);
        Assert.AreEqual(1, result.RemovedDayOffCount);
    }

    [TestMethod]
    public void SwapPlanner_MissingDutyIsRescuedFromRBeforeR2()
    {
        var fixture = CreateSwapFixture("R", includeSecondTechnicalDriver: true);
        fixture.SecondTechnicalDuty = CreateDuty(fixture.CompanyId, "R2", new TimeOnly(14, 0), new TimeOnly(22, 0), workMinutes: 480);
        fixture.Duties.Add(fixture.SecondTechnicalDuty);
        fixture.Context.Add(CreateAssignment(fixture.CompanyId, fixture.SecondTechnicalDriver!.Id, fixture.Date, new DateTime(2026, 8, 3, 14, 0, 0), new DateTime(2026, 8, 3, 22, 0, 0), duty: fixture.SecondTechnicalDuty));

        var result = fixture.Planner.RescueUnassignedDuties(fixture.Drivers, fixture.Duties, fixture.Schedules, fixture.Context, Array.Empty<PlanningDriverAvailability>(), new[] { fixture.Unassigned }, fixture.Date, fixture.Date, DefaultOptions, fixture.CompanyId, DateTime.UtcNow);

        Assert.AreEqual(1, result.RescuedDutyCount);
        Assert.AreEqual(1, result.RemovedReserveFirstShiftCount);
        Assert.AreEqual(0, result.RemovedReserveSecondShiftCount);
        Assert.IsTrue(fixture.Context.Any(x => x.DriverId == fixture.TechnicalDriver.Id && x.PlanningDutyId == fixture.MissingDuty.Id));
    }

    [TestMethod]
    public void SwapPlanner_ManualWgIsProtected()
    {
        var fixture = CreateSwapFixture("WG", technicalStatus: PlanningAssignmentStatus.Manual);
        var result = fixture.Planner.RescueUnassignedDuties(fixture.Drivers, fixture.Duties, fixture.Schedules, fixture.Context, Array.Empty<PlanningDriverAvailability>(), new[] { fixture.Unassigned }, fixture.Date, fixture.Date, DefaultOptions, fixture.CompanyId, DateTime.UtcNow);

        Assert.AreEqual(0, result.RescuedDutyCount);
        Assert.IsTrue(fixture.Context.Any(x => x.PlanningDutyId == fixture.TechnicalDuty.Id && x.Status == PlanningAssignmentStatus.Manual));
    }

    [TestMethod]
    public void SwapPlanner_AvailabilityIsProtected()
    {
        var fixture = CreateSwapFixture("WG");
        var vacation = CreateAvailability(fixture.CompanyId, fixture.TechnicalDriver.Id, fixture.Date, fixture.Date, PlanningDriverAvailabilityType.Vacation);

        var result = fixture.Planner.RescueUnassignedDuties(fixture.Drivers, fixture.Duties, fixture.Schedules, fixture.Context, new[] { vacation }, new[] { fixture.Unassigned }, fixture.Date, fixture.Date, DefaultOptions, fixture.CompanyId, DateTime.UtcNow);

        Assert.AreEqual(0, result.RescuedDutyCount);
        Assert.IsTrue(fixture.Context.Any(x => x.PlanningDutyId == fixture.TechnicalDuty.Id));
    }

    [TestMethod]
    public void SwapPlanner_RnIsNeverRemovedByRescue()
    {
        var fixture = CreateSwapFixture("RN");
        var result = fixture.Planner.RescueUnassignedDuties(fixture.Drivers, fixture.Duties, fixture.Schedules, fixture.Context, Array.Empty<PlanningDriverAvailability>(), new[] { fixture.Unassigned }, fixture.Date, fixture.Date, DefaultOptions, fixture.CompanyId, DateTime.UtcNow);

        Assert.AreEqual(0, result.RescuedDutyCount);
        Assert.IsTrue(fixture.Context.Any(x => x.PlanningDutyId == fixture.TechnicalDuty.Id));
    }

    [TestMethod]
    public void SwapPlanner_DirectSwapIsRejectedWhenTimeConflictWouldRemain()
    {
        var fixture = CreateSwapFixture("WG");
        var blockerDuty = CreateDuty(fixture.CompanyId, "BLOCK", new TimeOnly(9, 0), new TimeOnly(10, 0), workMinutes: 60);
        fixture.Context.Add(CreateAssignment(fixture.CompanyId, fixture.TechnicalDriver.Id, fixture.Date, new DateTime(2026, 8, 3, 9, 0, 0), new DateTime(2026, 8, 3, 10, 0, 0), duty: blockerDuty));

        var result = fixture.Planner.RescueUnassignedDuties(fixture.Drivers, fixture.Duties, fixture.Schedules, fixture.Context, Array.Empty<PlanningDriverAvailability>(), new[] { fixture.Unassigned }, fixture.Date, fixture.Date, DefaultOptions, fixture.CompanyId, DateTime.UtcNow);

        Assert.AreEqual(0, result.RescuedDutyCount);
        Assert.IsTrue(fixture.Context.Any(x => x.PlanningDutyId == fixture.TechnicalDuty.Id));
    }

    [TestMethod]
    public void SwapPlanner_DirectSwapIsRejectedWhenDailyRestWouldBeTooShort()
    {
        var fixture = CreateSwapFixture("WG");
        var previousDuty = CreateDuty(fixture.CompanyId, "PREV", new TimeOnly(0, 0), new TimeOnly(1, 0), workMinutes: 60);
        fixture.Context.Add(CreateAssignment(fixture.CompanyId, fixture.TechnicalDriver.Id, fixture.Date, new DateTime(2026, 8, 3, 0, 0, 0), new DateTime(2026, 8, 3, 1, 0, 0), duty: previousDuty));

        var result = fixture.Planner.RescueUnassignedDuties(fixture.Drivers, fixture.Duties, fixture.Schedules, fixture.Context, Array.Empty<PlanningDriverAvailability>(), new[] { fixture.Unassigned }, fixture.Date, fixture.Date, DefaultOptions, fixture.CompanyId, DateTime.UtcNow);

        Assert.AreEqual(0, result.RescuedDutyCount);
    }

    [TestMethod]
    public void SwapPlanner_ChainSwapCanMoveExistingDutyToTechnicalDriver()
    {
        var fixture = CreateSwapFixture("WG", includeSecondTechnicalDriver: true);
        var sourceDuty = CreateDuty(fixture.CompanyId, "SRC", new TimeOnly(14, 0), new TimeOnly(22, 0), workMinutes: 480);
        fixture.Duties.Add(sourceDuty);
        fixture.Context.Add(CreateAssignment(fixture.CompanyId, fixture.RegularDriver.Id, fixture.Date, new DateTime(2026, 8, 3, 14, 0, 0), new DateTime(2026, 8, 3, 22, 0, 0), duty: sourceDuty));
        var previousDuty = CreateDuty(fixture.CompanyId, "PREV", new TimeOnly(23, 0), new TimeOnly(23, 59), workMinutes: 59);
        fixture.Context.Add(CreateAssignment(fixture.CompanyId, fixture.TechnicalDriver.Id, fixture.Date.AddDays(-1), new DateTime(2026, 8, 2, 23, 0, 0), new DateTime(2026, 8, 2, 23, 59, 0), duty: previousDuty));

        var result = fixture.Planner.RescueUnassignedDuties(fixture.Drivers, fixture.Duties, fixture.Schedules, fixture.Context, Array.Empty<PlanningDriverAvailability>(), new[] { fixture.Unassigned }, fixture.Date, fixture.Date, DefaultOptions, fixture.CompanyId, DateTime.UtcNow);

        Assert.AreEqual(1, result.RescuedDutyCount);
        Assert.AreEqual(1, result.ChainSwapCount);
        Assert.IsTrue(fixture.Context.Any(x => x.DriverId == fixture.RegularDriver.Id && x.PlanningDutyId == fixture.MissingDuty.Id));
        Assert.IsTrue(fixture.Context.Any(x => x.DriverId == fixture.TechnicalDriver.Id && x.PlanningDutyId == sourceDuty.Id));
    }

    [TestMethod]
    public void SwapPlanner_FailedChainLeavesOriginalAssignmentsUntouched()
    {
        var fixture = CreateSwapFixture("WG", includeSecondTechnicalDriver: true);
        var sourceDuty = CreateDuty(fixture.CompanyId, "SRC", new TimeOnly(14, 0), new TimeOnly(22, 0), workMinutes: 480);
        fixture.Duties.Add(sourceDuty);
        var original = CreateAssignment(fixture.CompanyId, fixture.RegularDriver.Id, fixture.Date, new DateTime(2026, 8, 3, 14, 0, 0), new DateTime(2026, 8, 3, 22, 0, 0), duty: sourceDuty);
        fixture.Context.Add(original);
        var blockerDuty = CreateDuty(fixture.CompanyId, "BLOCK", new TimeOnly(13, 0), new TimeOnly(15, 0), workMinutes: 120);
        fixture.Context.Add(CreateAssignment(fixture.CompanyId, fixture.TechnicalDriver.Id, fixture.Date, new DateTime(2026, 8, 3, 13, 0, 0), new DateTime(2026, 8, 3, 15, 0, 0), duty: blockerDuty));

        var result = fixture.Planner.RescueUnassignedDuties(fixture.Drivers, fixture.Duties, fixture.Schedules, fixture.Context, Array.Empty<PlanningDriverAvailability>(), new[] { fixture.Unassigned }, fixture.Date, fixture.Date, DefaultOptions, fixture.CompanyId, DateTime.UtcNow);

        Assert.AreEqual(0, result.RescuedDutyCount);
        Assert.IsTrue(fixture.Context.Any(x => x.Id == original.Id && x.DriverId == fixture.RegularDriver.Id && x.PlanningDutyId == sourceDuty.Id));
        Assert.IsTrue(fixture.Context.Any(x => x.PlanningDutyId == fixture.TechnicalDuty.Id));
    }
    [TestMethod]
    public void Eligibility_ForbiddenDutyRejectsDriver()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "12", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var options = DefaultOptions with
        {
            AssignmentRules = new[]
            {
                new PlanningAssignmentRule
                {
                    CompanyId = companyId,
                    DriverId = driver.Id,
                    DutyId = duty.Id,
                    Type = PlanningAssignmentRuleType.Forbidden,
                    Note = "test"
                }
            }
        };

        var evaluation = Evaluate(driver, duty, new DateOnly(2026, 8, 3), Array.Empty<PlanningAssignment>(), options: options);

        Assert.IsFalse(evaluation.IsEligible);
        CollectionAssert.Contains(evaluation.RejectionReasons, PlanningCandidateRejectionReason.DriverDutyForbidden);
        CollectionAssert.Contains(evaluation.ConstraintMatches, "Zakaz: 12 (test)");
    }

    [TestMethod]
    public void Eligibility_ForbiddenRuleFromOtherCompanyDoesNotRejectDriver()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "12", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var options = DefaultOptions with
        {
            AssignmentRules = new[]
            {
                new PlanningAssignmentRule
                {
                    CompanyId = Guid.NewGuid(),
                    DriverId = driver.Id,
                    DutyId = duty.Id,
                    Type = PlanningAssignmentRuleType.Forbidden
                }
            }
        };

        var evaluation = Evaluate(driver, duty, new DateOnly(2026, 8, 3), Array.Empty<PlanningAssignment>(), options: options);

        Assert.IsTrue(evaluation.IsEligible);
        CollectionAssert.DoesNotContain(evaluation.RejectionReasons, PlanningCandidateRejectionReason.DriverDutyForbidden);
    }

    [TestMethod]
    public void Evaluator_PreferredDriverWinsEquivalentCandidate()
    {
        var companyId = Guid.NewGuid();
        var normalDriver = CreateDriver(companyId, "Adam", "Normal", Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var preferredDriver = CreateDriver(companyId, "Beata", "Preferred", Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var duty = CreateDuty(companyId, "15", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var options = DefaultOptions with
        {
            AssignmentRules = new[]
            {
                new PlanningAssignmentRule
                {
                    CompanyId = companyId,
                    DriverId = preferredDriver.Id,
                    DutyNumber = "15",
                    Type = PlanningAssignmentRuleType.Preferred
                }
            }
        };
        var evaluator = new PlanningCandidateEvaluator(new PlanningEligibilityChecker());

        var evaluations = evaluator.EvaluateCandidates(new[] { normalDriver, preferredDriver }, duty, new DateOnly(2026, 8, 3), Array.Empty<PlanningAssignment>(), Array.Empty<PlanningDriverAvailability>(), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), options);
        var selected = evaluator.ChooseBestCandidate(evaluations);

        Assert.IsNotNull(selected);
        Assert.AreEqual(preferredDriver.Id, selected.DriverId);
        Assert.IsTrue(selected.MatchesPreference);
        Assert.IsTrue(selected.ScoreBreakdown.PreferenceScore < 0);
    }

    [TestMethod]
    public void Evaluator_PreferenceDoesNotOverrideVacation()
    {
        var companyId = Guid.NewGuid();
        var preferredDriver = CreateDriver(companyId, "Adam", "Preferred", Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var availableDriver = CreateDriver(companyId, "Beata", "Available", Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var duty = CreateDuty(companyId, "15", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var date = new DateOnly(2026, 8, 3);
        var options = DefaultOptions with
        {
            AssignmentRules = new[]
            {
                new PlanningAssignmentRule
                {
                    CompanyId = companyId,
                    DriverId = preferredDriver.Id,
                    DutyId = duty.Id,
                    Type = PlanningAssignmentRuleType.Preferred
                }
            }
        };
        var vacation = CreateAvailability(companyId, preferredDriver.Id, date, date, PlanningDriverAvailabilityType.Vacation);
        var evaluator = new PlanningCandidateEvaluator(new PlanningEligibilityChecker());

        var evaluations = evaluator.EvaluateCandidates(new[] { preferredDriver, availableDriver }, duty, date, Array.Empty<PlanningAssignment>(), new[] { vacation }, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), options);
        var selected = evaluator.ChooseBestCandidate(evaluations);

        Assert.IsNotNull(selected);
        Assert.AreEqual(availableDriver.Id, selected.DriverId);
        CollectionAssert.Contains(evaluations.Single(x => x.DriverId == preferredDriver.Id).RejectionReasons, PlanningCandidateRejectionReason.Vacation);
    }

    [TestMethod]
    public void SwapPlanner_ForbiddenDutyBlocksDirectRescue()
    {
        var fixture = CreateSwapFixture("WG");
        var options = DefaultOptions with
        {
            AssignmentRules = new[]
            {
                new PlanningAssignmentRule
                {
                    CompanyId = fixture.CompanyId,
                    DriverId = fixture.TechnicalDriver.Id,
                    DutyId = fixture.MissingDuty.Id,
                    Type = PlanningAssignmentRuleType.Forbidden
                }
            }
        };

        var result = fixture.Planner.RescueUnassignedDuties(fixture.Drivers, fixture.Duties, fixture.Schedules, fixture.Context, Array.Empty<PlanningDriverAvailability>(), new[] { fixture.Unassigned }, fixture.Date, fixture.Date, options, fixture.CompanyId, DateTime.UtcNow);

        Assert.AreEqual(0, result.RescuedDutyCount);
        Assert.IsTrue(fixture.Context.Any(x => x.PlanningDutyId == fixture.TechnicalDuty.Id));
    }

    [TestMethod]
    public void SwapPlanner_ForbiddenDutyBlocksChainReceiver()
    {
        var fixture = CreateSwapFixture("WG", includeSecondTechnicalDriver: true);
        var sourceDuty = CreateDuty(fixture.CompanyId, "SRC", new TimeOnly(14, 0), new TimeOnly(22, 0), workMinutes: 480);
        fixture.Duties.Add(sourceDuty);
        fixture.Context.Add(CreateAssignment(fixture.CompanyId, fixture.RegularDriver.Id, fixture.Date, new DateTime(2026, 8, 3, 14, 0, 0), new DateTime(2026, 8, 3, 22, 0, 0), duty: sourceDuty));
        var previousDuty = CreateDuty(fixture.CompanyId, "PREV", new TimeOnly(23, 0), new TimeOnly(23, 59), workMinutes: 59);
        fixture.Context.Add(CreateAssignment(fixture.CompanyId, fixture.TechnicalDriver.Id, fixture.Date.AddDays(-1), new DateTime(2026, 8, 2, 23, 0, 0), new DateTime(2026, 8, 2, 23, 59, 0), duty: previousDuty));
        var options = DefaultOptions with
        {
            AssignmentRules = new[]
            {
                new PlanningAssignmentRule
                {
                    CompanyId = fixture.CompanyId,
                    DriverId = fixture.TechnicalDriver.Id,
                    DutyId = sourceDuty.Id,
                    Type = PlanningAssignmentRuleType.Forbidden
                }
            }
        };

        var result = fixture.Planner.RescueUnassignedDuties(fixture.Drivers, fixture.Duties, fixture.Schedules, fixture.Context, Array.Empty<PlanningDriverAvailability>(), new[] { fixture.Unassigned }, fixture.Date, fixture.Date, options, fixture.CompanyId, DateTime.UtcNow);

        Assert.AreEqual(0, result.RescuedDutyCount);
        Assert.IsTrue(fixture.Context.Any(x => x.DriverId == fixture.RegularDriver.Id && x.PlanningDutyId == sourceDuty.Id));
        Assert.IsTrue(fixture.Context.Any(x => x.PlanningDutyId == fixture.TechnicalDuty.Id));
    }

    [TestMethod]
    public void GenerationDiagnostics_SeparatesUnassignedDutiesFromRejectedCandidatesAndTimeConflicts()
    {
        var result = new PlanningAutoGenerateResultDto
        {
            UnassignedDuties = new List<PlanningUnassignedDutyDto>
            {
                new()
                {
                    DutyId = Guid.NewGuid(),
                    DutyNumber = "12",
                    Date = new DateOnly(2026, 8, 10),
                    CandidateEvaluations = new List<PlanningCandidateEvaluationDto>
                    {
                        new() { IsEligible = false, RejectionReasons = new List<string> { PlanningCandidateRejectionReason.OverlappingAssignment.ToString() } },
                        new() { IsEligible = false, RejectionReasons = new List<string> { PlanningCandidateRejectionReason.InsufficientDailyRestBefore.ToString(), PlanningCandidateRejectionReason.DriverDutyForbidden.ToString() } },
                        new() { IsEligible = true }
                    }
                },
                new()
                {
                    DutyId = Guid.NewGuid(),
                    DutyNumber = "13",
                    Date = new DateOnly(2026, 8, 10),
                    CandidateEvaluations = new List<PlanningCandidateEvaluationDto>
                    {
                        new() { IsEligible = false, RejectionReasons = new List<string> { PlanningCandidateRejectionReason.InsufficientWeeklyRest.ToString() } }
                    }
                }
            }
        };
        result.UnassignedCount = result.UnassignedDuties.Count;

        PlanningAutoGeneratorService.SetRejectionDiagnostics(result);

        Assert.AreEqual(2, result.UnassignedCount);
        Assert.AreEqual(3, result.CandidateRejectionCount);
        Assert.AreEqual(1, result.TimeConflictRejectionCount);
        Assert.AreEqual(1, result.ConflictCount);
        Assert.AreEqual(1, result.DailyRestRejectionCount);
        Assert.AreEqual(1, result.WeeklyRestRejectionCount);
    }
    [TestMethod]
    public void DutyDayAvailability_NullMaskDefaultsToWeekdays()
    {
        var duty = CreateDuty(Guid.NewGuid(), "10", new TimeOnly(8, 0), new TimeOnly(16, 0));

        Assert.IsTrue(PlanningDutyDayAvailability.IsActiveOn(duty, new DateOnly(2026, 8, 3)));
        Assert.IsFalse(PlanningDutyDayAvailability.IsActiveOn(duty, new DateOnly(2026, 8, 8)));
    }

    [TestMethod]
    public void DutyDayAvailability_WeekendMaskAllowsOnlyWeekend()
    {
        var duty = CreateDuty(Guid.NewGuid(), "10", new TimeOnly(8, 0), new TimeOnly(16, 0));
        duty.ActiveDaysMask = PlanningDutyDayAvailability.WeekendMask;

        Assert.IsFalse(PlanningDutyDayAvailability.IsActiveOn(duty, new DateOnly(2026, 8, 3)));
        Assert.IsTrue(PlanningDutyDayAvailability.IsActiveOn(duty, new DateOnly(2026, 8, 8)));
    }

    [TestMethod]
    public void DutyDayAvailability_ZeroMaskMeansNoPlannedDays()
    {
        var duty = CreateDuty(Guid.NewGuid(), "10", new TimeOnly(8, 0), new TimeOnly(16, 0));
        duty.ActiveDaysMask = 0;

        Assert.IsFalse(PlanningDutyDayAvailability.IsActiveOn(duty, new DateOnly(2026, 8, 3)));
        Assert.IsFalse(PlanningDutyDayAvailability.IsActiveOn(duty, new DateOnly(2026, 8, 8)));
    }

    [TestMethod]
    public void DutyDayAvailability_IncludeHolidaysAllowsHolidayOutsideMask()
    {
        var duty = CreateDuty(Guid.NewGuid(), "10", new TimeOnly(8, 0), new TimeOnly(16, 0));
        duty.ActiveDaysMask = 0;
        duty.IncludeHolidays = true;

        Assert.IsTrue(PlanningDutyDayAvailability.IsActiveOn(duty, new DateOnly(2026, 8, 15), isHoliday: true));
    }
    [TestMethod]
    public void AutoPlanningDriverFilter_IncludesPlanningEnabledDriverWhenRequestIsEmpty()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");

        Assert.IsTrue(PlanningAutoGeneratorService.IsDriverEligibleForAutoPlanning(driver, companyId, Array.Empty<Guid>()));
    }

    [TestMethod]
    public void AutoPlanningDriverFilter_ExcludesPlanningDisabledDriver()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        driver.IncludeInPlanning = false;

        Assert.IsFalse(PlanningAutoGeneratorService.IsDriverEligibleForAutoPlanning(driver, companyId, Array.Empty<Guid>()));
    }

    [TestMethod]
    public void AutoPlanningDriverFilter_RequestDriverIdsOnlyNarrowPlanningEnabledDrivers()
    {
        var companyId = Guid.NewGuid();
        var included = CreateDriver(companyId, "Adam", "Nowak");
        var notRequested = CreateDriver(companyId, "Ewa", "Kowalska");
        var disabledRequested = CreateDriver(companyId, "Jan", "Zielinski");
        disabledRequested.IncludeInPlanning = false;
        var requestedIds = new[] { included.Id, disabledRequested.Id };

        Assert.IsTrue(PlanningAutoGeneratorService.IsDriverEligibleForAutoPlanning(included, companyId, requestedIds));
        Assert.IsFalse(PlanningAutoGeneratorService.IsDriverEligibleForAutoPlanning(notRequested, companyId, requestedIds));
        Assert.IsFalse(PlanningAutoGeneratorService.IsDriverEligibleForAutoPlanning(disabledRequested, companyId, requestedIds));
    }

    [TestMethod]
    public void AutoPlanningDriverFilter_ExcludesOtherCompanyDriver()
    {
        var companyId = Guid.NewGuid();
        var otherCompanyDriver = CreateDriver(Guid.NewGuid(), "Adam", "Nowak");

        Assert.IsFalse(PlanningAutoGeneratorService.IsDriverEligibleForAutoPlanning(otherCompanyDriver, companyId, Array.Empty<Guid>()));
    }

    [TestMethod]
    public void CandidateEvaluation_IndexedDriverContextMatchesCollectionBasedEvaluation()
    {
        var companyId = Guid.NewGuid();
        var availableDriver = CreateDriver(companyId, "Available", "Driver");
        var busyDriver = CreateDriver(companyId, "Busy", "Driver");
        var absentDriver = CreateDriver(companyId, "Absent", "Driver");
        var drivers = new[] { availableDriver, busyDriver, absentDriver };
        var date = new DateOnly(2026, 8, 10);
        var duty = CreateDuty(companyId, "D1", new TimeOnly(8, 0), new TimeOnly(16, 0), workMinutes: 480);
        var assignments = new[]
        {
            CreateAssignment(companyId, busyDriver.Id, date, new DateTime(2026, 8, 10, 7, 0, 0), new DateTime(2026, 8, 10, 12, 0, 0)),
            CreateAssignment(companyId, availableDriver.Id, date.AddDays(-2), new DateTime(2026, 8, 8, 8, 0, 0), new DateTime(2026, 8, 8, 16, 0, 0))
        };
        var availabilities = new[]
        {
            new PlanningDriverAvailability
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                DriverId = absentDriver.Id,
                DateFrom = date,
                DateTo = date,
                Type = PlanningDriverAvailabilityType.Vacation
            }
        };
        var assignmentsByDriver = assignments.GroupBy(x => x.DriverId).ToDictionary(x => x.Key, x => x.ToList());
        var availabilitiesByDriver = availabilities.GroupBy(x => x.DriverId).ToDictionary(x => x.Key, x => x.ToList());
        var evaluator = new PlanningCandidateEvaluator(new PlanningEligibilityChecker());
        var calendarService = new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider());
        var dateFrom = new DateOnly(2026, 8, 1);
        var dateTo = new DateOnly(2026, 8, 31);

        var collectionBased = evaluator.EvaluateCandidates(
            drivers, duty, date, assignments, availabilities, dateFrom, dateTo, DefaultOptions, calendarService);
        var indexed = evaluator.EvaluateCandidates(
            drivers, duty, date, assignmentsByDriver, availabilitiesByDriver, dateFrom, dateTo, DefaultOptions, calendarService);

        Assert.AreEqual(collectionBased.Count, indexed.Count);
        for (var index = 0; index < collectionBased.Count; index++)
        {
            var expected = collectionBased[index];
            var actual = indexed[index];
            Assert.AreEqual(expected.DriverId, actual.DriverId);
            Assert.AreEqual(expected.IsEligible, actual.IsEligible);
            Assert.AreEqual(expected.Score, actual.Score);
            Assert.AreEqual(expected.MonthlyWorkMinutesBefore, actual.MonthlyWorkMinutesBefore);
            Assert.AreEqual(expected.WeeklyWorkMinutesBefore, actual.WeeklyWorkMinutesBefore);
            Assert.AreEqual(expected.ConsecutiveWorkDaysAfter, actual.ConsecutiveWorkDaysAfter);
            CollectionAssert.AreEqual(expected.RejectionReasons, actual.RejectionReasons);
        }

        Assert.AreEqual(
            evaluator.ChooseBestCandidate(collectionBased)?.DriverId,
            evaluator.ChooseBestCandidate(indexed)?.DriverId);
    }
    private static PlanningCandidateEvaluation Evaluate(
        Driver driver,
        PlanningDuty duty,
        DateOnly date,
        IReadOnlyCollection<PlanningAssignment> assignments,
        IReadOnlyCollection<PlanningDriverAvailability>? availabilities = null,
        PlanningGenerationOptions? options = null)
    {
        PlanningWorkInterval? interval = PlanningWorkInterval.TryCreate(date, duty, out var createdInterval)
            ? createdInterval
            : null;
        var candidateWorkMinutes = interval.HasValue
            ? PlanningDutyWorkMinutesResolver.Resolve(duty, interval.Value).WorkMinutes
            : 0;
        return new PlanningEligibilityChecker().Evaluate(
            driver,
            duty,
            date,
            interval,
            candidateWorkMinutes,
            assignments,
            availabilities ?? Array.Empty<PlanningDriverAvailability>(),
            options ?? DefaultOptions,
            new DateOnly(date.Year, date.Month, 1),
            new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)));
    }

    private static Driver CreateDriver(Guid companyId, string firstName, string lastName, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        CompanyId = companyId,
        FirstName = firstName,
        LastName = lastName,
        CardNumber = Guid.NewGuid().ToString("N")
    };

    private static PlanningDuty CreateDuty(
        Guid companyId,
        string dutyNumber,
        TimeOnly start,
        TimeOnly end,
        int? workMinutes = null,
        int? totalDurationMinutes = null) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        DutyNumber = dutyNumber,
        Name = $"Służba {dutyNumber}",
        StartTime = start,
        EndTime = end,
        WorkMinutes = workMinutes,
        TotalDurationMinutes = totalDurationMinutes
    };

    private static PlanningAssignment CreateAssignment(
        Guid companyId,
        Guid driverId,
        DateOnly date,
        DateTime start,
        DateTime end,
        PlanningAssignmentStatus status = PlanningAssignmentStatus.Generated,
        PlanningAssignmentType assignmentType = PlanningAssignmentType.Duty,
        PlanningDuty? duty = null) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        PlanningScheduleId = Guid.NewGuid(),
        DriverId = driverId,
        Date = date,
        StartDateTime = start,
        EndDateTime = end,
        Status = status,
        AssignmentType = assignmentType,
        PlanningDutyId = duty?.Id,
        PlanningDuty = duty
    };

    private static SwapFixture CreateSwapFixture(
        string technicalCode,
        PlanningAssignmentStatus technicalStatus = PlanningAssignmentStatus.Generated,
        bool includeSecondTechnicalDriver = false)
    {
        var companyId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 3);
        var regularDriver = CreateDriver(companyId, "Adam", "Regular");
        var technicalDriver = CreateDriver(companyId, "Beata", "Technical");
        var secondTechnicalDriver = includeSecondTechnicalDriver ? CreateDriver(companyId, "Celina", "Technical") : null;
        var drivers = secondTechnicalDriver is null
            ? new List<Driver> { regularDriver, technicalDriver }
            : new List<Driver> { regularDriver, technicalDriver, secondTechnicalDriver };
        var missingDuty = CreateDuty(companyId, "MISS", new TimeOnly(8, 0), new TimeOnly(12, 0), workMinutes: 240);
        var technicalDuty = technicalCode switch
        {
            "WG" => CreateDuty(companyId, "WG", new TimeOnly(0, 0), new TimeOnly(0, 1)),
            "W" => CreateDuty(companyId, "W", new TimeOnly(0, 0), new TimeOnly(0, 1)),
            "R" => CreateDuty(companyId, "R", new TimeOnly(8, 0), new TimeOnly(16, 0), workMinutes: 480),
            "R2" => CreateDuty(companyId, "R2", new TimeOnly(14, 0), new TimeOnly(22, 0), workMinutes: 480),
            "RN" => CreateDuty(companyId, "RN", new TimeOnly(20, 0), new TimeOnly(6, 0), workMinutes: 480),
            _ => CreateDuty(companyId, technicalCode, new TimeOnly(0, 0), new TimeOnly(0, 1))
        };
        var schedule = CreateSchedule(companyId, date.Year, date.Month);
        var context = new List<PlanningAssignment>
        {
            CreateAssignment(
                companyId,
                technicalDriver.Id,
                date,
                date.ToDateTime(technicalDuty.StartTime!.Value),
                date.ToDateTime(technicalDuty.EndTime!.Value).AddDays(technicalCode == "RN" ? 1 : 0),
                technicalStatus,
                PlanningAssignmentType.Duty,
                technicalDuty)
        };

        var duties = new List<PlanningDuty> { missingDuty, technicalDuty };
        return new SwapFixture
        {
            CompanyId = companyId,
            Date = date,
            RegularDriver = regularDriver,
            TechnicalDriver = technicalDriver,
            SecondTechnicalDriver = secondTechnicalDriver,
            Drivers = drivers,
            MissingDuty = missingDuty,
            TechnicalDuty = technicalDuty,
            Duties = duties,
            Schedules = new Dictionary<(int Year, int Month), PlanningSchedule> { [(date.Year, date.Month)] = schedule },
            Context = context,
            Unassigned = new PlanningUnassignedDutyDto
            {
                DutyId = missingDuty.Id,
                DutyNumber = missingDuty.DutyNumber,
                Date = date,
                StartDateTime = new DateTime(2026, 8, 3, 8, 0, 0),
                EndDateTime = new DateTime(2026, 8, 3, 12, 0, 0),
                Summary = "brak"
            },
            Planner = new PlanningAssignmentSwapPlanner(
                new PlanningEligibilityChecker(),
                new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider()))
        };
    }
    private static PlanningSchedule CreateSchedule(Guid companyId, int year, int month) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        Name = $"Plan {year}-{month:00}",
        Year = year,
        Month = month,
        CreatedUtc = DateTime.UtcNow
    };
    private static PlanningDriverAvailability CreateAvailability(
        Guid companyId,
        Guid driverId,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningDriverAvailabilityType type) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        DriverId = driverId,
        DateFrom = dateFrom,
        DateTo = dateTo,
        Type = type
    };
    private sealed class SwapFixture
    {
        public Guid CompanyId { get; init; }
        public DateOnly Date { get; init; }
        public Driver RegularDriver { get; init; } = null!;
        public Driver TechnicalDriver { get; init; } = null!;
        public Driver? SecondTechnicalDriver { get; init; }
        public List<Driver> Drivers { get; init; } = new();
        public PlanningDuty MissingDuty { get; init; } = null!;
        public PlanningDuty TechnicalDuty { get; init; } = null!;
        public PlanningDuty? SecondTechnicalDuty { get; set; }
        public List<PlanningDuty> Duties { get; init; } = new();
        public Dictionary<(int Year, int Month), PlanningSchedule> Schedules { get; init; } = new();
        public List<PlanningAssignment> Context { get; init; } = new();
        public PlanningUnassignedDutyDto Unassigned { get; init; } = null!;
        public PlanningAssignmentSwapPlanner Planner { get; init; } = null!;
    }
}
