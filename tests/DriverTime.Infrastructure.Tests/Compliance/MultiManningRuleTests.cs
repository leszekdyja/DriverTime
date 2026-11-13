using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Compliance.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class MultiManningRuleTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid DriverId = Guid.NewGuid();
    private static readonly DailyRestViolationRule DailyRest = new(NullLogger<DailyRestViolationRule>.Instance);
    private static readonly ContinuousDrivingBreakRule Breaks = new(NullLogger<ContinuousDrivingBreakRule>.Instance);

    private static TimelineActivity Activity(string type, double start, double end, bool? crew = true, int? slot = 1) => new()
    {
        SourceActivityId = Guid.NewGuid(), DriverId = DriverId,
        ActivityType = type, StartUtc = Start.AddMinutes(start), EndUtc = Start.AddMinutes(end),
        IsCrew = crew, CardSlot = slot
    };

    private static List<TimelineActivity> CrewDuty(int restStart = 21 * 60) =>
    [
        Activity("WORK", 0, 60, false),
        Activity("DRIVING", 60, 330),
        Activity("AVAILABILITY", 330, 600, slot: 2),
        Activity("WORK", 600, 720),
        Activity("DRIVING", 720, 990),
        Activity("AVAILABILITY", 990, restStart, slot: 2),
        Activity("REST", restStart, restStart + 540, null, null)
    ];

    [TestMethod]
    public void Crew_NineHourRestCompletedExactlyAtThirtyHours_IsAccepted()
    {
        Assert.AreEqual(0, DailyRest.Evaluate(DriverId, CrewDuty()).Violations.Count);
        Assert.AreEqual(0, Breaks.Evaluate(DriverId, CrewDuty()).Violations.Count);
    }

    [TestMethod]
    public void Crew_RestCompletedOneMinuteAfterDeadline_IsViolation()
    {
        var violation = DailyRest.Evaluate(DriverId, CrewDuty(1261)).Violations.Single();
        Assert.AreEqual(30L, violation.Metadata["analysisWindowHours"]);
        Assert.AreEqual(539L, violation.ActualMinutes);
        Assert.AreEqual(540L, violation.LimitMinutes);
        Assert.IsTrue(violation.Description.Contains("30h"));
        Assert.AreEqual("HIGH", violation.Severity);
    }

    [TestMethod]
    public void Crew_AvailabilityIsNotDailyRest()
    {
        var duty = CrewDuty();
        duty[^1].ActivityType = "AVAILABILITY";
        duty[^1].IsCrew = true;
        duty[^1].CardSlot = 2;
        Assert.IsTrue(DailyRest.Evaluate(DriverId, duty).Violations.Any(x => x.ActualMinutes == 0));
    }

    [TestMethod]
    public void Crew_SecondDriverMayJoinWithinFirstHour()
    {
        var duty = CrewDuty();
        duty[0].ActivityType = "DRIVING";
        Assert.AreEqual(0, DailyRest.Evaluate(DriverId, duty).Violations.Count);
    }

    [TestMethod]
    public void Crew_SoloDrivingAfterFirstHourDoesNotGrantThirtyHours()
    {
        var duty = CrewDuty();
        duty[4].IsCrew = false;
        Assert.IsTrue(DailyRest.Evaluate(DriverId, duty).Violations.Any(x => Equals(x.Metadata["analysisWindowHours"], 24L)));
    }

    [TestMethod]
    public void Crew_JoiningAfterFirstHourDoesNotGrantThirtyHours()
    {
        var duty = CrewDuty();
        duty[0].EndUtc = Start.AddMinutes(61);
        duty[1].StartUtc = duty[0].EndUtc;
        Assert.IsTrue(DailyRest.Evaluate(DriverId, duty).Violations.Any(x => Equals(x.Metadata["analysisWindowHours"], 24L)));
    }

    [TestMethod]
    public void Crew_PartiallyMissingFlagsRequiresVerification()
    {
        var duty = CrewDuty();
        duty[4].IsCrew = null;
        var violation = DailyRest.Evaluate(DriverId, duty).Violations.First();
        Assert.AreEqual("WARNING", violation.Severity);
        Assert.AreEqual("true", violation.Metadata["requiresVerification"]);
    }

    [TestMethod]
    public void Crew_NextSoloDutyStillUsesTwentyFourHours()
    {
        var duty = CrewDuty();
        duty.Add(Activity("DRIVING", 1800, 3060, false));
        duty.Add(Activity("REST", 3060, 3600, null, null));
        Assert.IsTrue(DailyRest.Evaluate(DriverId, duty).Violations.All(x => Equals(x.Metadata["analysisWindowHours"], 24L)));
        Assert.IsTrue(DailyRest.Evaluate(DriverId, duty).Violations.Count > 0);
    }

    [DataTestMethod]
    [DataRow(true, 2, 45, 0)]
    [DataRow(true, 1, 45, 1)]
    [DataRow(false, 2, 45, 1)]
    [DataRow(true, 2, 44, 1)]
    public void Availability_OnlyCrewCoDriverWithFortyFiveMinutesResets(bool crew, int slot, int minutes, int count)
    {
        var result = Breaks.Evaluate(DriverId,
        [
            Activity("DRIVING", 0, 270),
            Activity("AVAILABILITY", 270, 270 + minutes, crew, slot),
            Activity("DRIVING", 270 + minutes, 330 + minutes)
        ]);
        Assert.AreEqual(count, result.Violations.Count);
    }

    [TestMethod]
    public void Crew_TwoAvailabilityFragmentsFifteenAndThirtyDoNotPretendFullBreak()
    {
        Assert.AreEqual(1, Breaks.Evaluate(DriverId,
        [
            Activity("DRIVING", 0, 240), Activity("AVAILABILITY", 240, 255, slot: 2),
            Activity("DRIVING", 255, 285), Activity("AVAILABILITY", 285, 315, slot: 2),
            Activity("DRIVING", 315, 375)
        ]).Violations.Count);
    }

    [TestMethod]
    public void OldImportsWithoutCrewFlagsDoNotReceiveThirtyHourException()
    {
        var duty = CrewDuty();
        foreach (var activity in duty) { activity.IsCrew = null; activity.CardSlot = null; }
        Assert.IsTrue(DailyRest.Evaluate(DriverId, duty).Violations.Any(x => Equals(x.Metadata["analysisWindowHours"], 24L)));
    }

    [TestMethod]
    public void AdjacentCoDriverAvailabilityAcrossMidnightFormsOneBreak()
    {
        Assert.AreEqual(0, Breaks.Evaluate(DriverId,
        [
            Activity("DRIVING", 1140, 1410),
            Activity("AVAILABILITY", 1410, 1440, slot: 2),
            Activity("AVAILABILITY", 1440, 1455, slot: 2),
            Activity("DRIVING", 1455, 1515)
        ]).Violations.Count);
    }

    [TestMethod]
    public void RestBreakIsContinuousDespiteCrewFlagChange()
    {
        Assert.AreEqual(0, Breaks.Evaluate(DriverId,
        [
            Activity("DRIVING", 0, 270), Activity("REST", 270, 290),
            Activity("REST", 290, 315, false), Activity("DRIVING", 315, 375)
        ]).Violations.Count);
    }

    [TestMethod]
    public void Crew_DailyDrivingLimitDoesNotBecomeTwentyHours()
    {
        var rule = new DailyDrivingLimitRule(NullLogger<DailyDrivingLimitRule>.Instance);
        Assert.IsTrue(rule.Evaluate(DriverId,
        [Activity("DRIVING", 0, 270), Activity("AVAILABILITY", 270, 540, slot: 2),
         Activity("DRIVING", 540, 810), Activity("AVAILABILITY", 810, 855, slot: 2),
         Activity("DRIVING", 855, 916)]).Violations.Any(x => x.ActualMinutes == 601));
    }

    [TestMethod]
    public void NormalizeTimeline_PreservesCrewAndSlotBoundaries()
    {
        var timeline = TimelineBuilderService.NormalizeTimeline(
        [Activity("AVAILABILITY", 0, 30, false), Activity("AVAILABILITY", 30, 60, true, 2)]);
        Assert.AreEqual(2, timeline.Count);
        Assert.AreEqual(false, timeline[0].IsCrew);
        Assert.AreEqual(true, timeline[1].IsCrew);
        Assert.AreEqual(2, timeline[1].CardSlot);
    }

    [TestMethod]
    public void NormalizeTimeline_EnrichedMetadataWinsOverOldDuplicateAndSurvivesSplitting()
    {
        var old = Activity("AVAILABILITY", 0, 90, null, null);
        var enriched = Activity("AVAILABILITY", 0, 90, true, 2);
        var timeline = TimelineBuilderService.NormalizeTimeline([old, enriched, Activity("REST", 30, 60)]);
        Assert.AreEqual(3, timeline.Count);
        Assert.IsTrue(timeline.Where(x => x.ActivityType == "AVAILABILITY").All(x => x.IsCrew == true && x.CardSlot == 2));
    }

    [TestMethod]
    public void Crew_GapInDutyCannotBeUsedAsConfirmedDailyRest()
    {
        var duty = CrewDuty();
        duty.RemoveAt(2);
        var violation = DailyRest.Evaluate(DriverId, duty).Violations.First();
        Assert.AreEqual("WARNING", violation.Severity);
        Assert.AreEqual("Uncertain", violation.Metadata["multiManning"]);
    }

    [TestMethod]
    public void Crew_DutyStartsWithCoDriverAvailabilityNotFirstOwnDriving()
    {
        var duty = CrewDuty(1261);
        duty[0].ActivityType = "AVAILABILITY";
        duty[0].IsCrew = true;
        duty[0].CardSlot = 2;
        var violation = DailyRest.Evaluate(DriverId, duty).Violations.Single();
        Assert.AreEqual(Start, violation.PeriodStartUtc);
        Assert.AreEqual(539L, violation.ActualMinutes);
    }

    [TestMethod]
    public void LegacyDriverEndpointUsesSameCrewRules()
    {
        var file = new DriverTime.Domain.Entities.DddFile { Id = Guid.NewGuid(), DriverId = DriverId };
        var activities = CrewDuty().Select(a => new DriverTime.Domain.Entities.DriverActivity
        {
            Id = a.SourceActivityId, DddFile = file, DddFileId = file.Id,
            StartUtc = a.StartUtc, EndUtc = a.EndUtc, ActivityType = a.ActivityType,
            IsCrew = a.IsCrew, CardSlot = a.CardSlot
        }).ToList();
        var method = typeof(DriverTime.Infrastructure.Services.DriverViolationService).GetMethod(
            "CalculateViolations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var violations = (IReadOnlyList<DriverTime.Application.Violations.DTOs.DriverViolationDto>)method.Invoke(null, [activities])!;
        Assert.IsFalse(violations.Any(x => x.Code.Contains("DAILY_REST") || x.Code.Contains("CONTINUOUS_DRIVING")));
    }

    [TestMethod]
    public void Crew_NineHourMinimumDoesNotConsumeSoloReducedRestAllowance()
    {
        var timeline = new List<TimelineActivity>();
        for (var day = 0; day < 4; day++)
            foreach (var activity in CrewDuty())
            {
                activity.StartUtc = activity.StartUtc.AddHours(day * 30);
                activity.EndUtc = activity.EndUtc.AddHours(day * 30);
                timeline.Add(activity);
            }
        var rule = new ReducedDailyRestCounterRule(NullLogger<ReducedDailyRestCounterRule>.Instance);
        Assert.AreEqual(0, rule.Evaluate(DriverId, timeline).Violations.Count);
    }
}
