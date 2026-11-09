using DriverTime.Application.Planning;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class PlanningChristmasWorkLimitTests
{
    [TestMethod]
    public void AnyOtherChristmasWorkdayBlocksAutomaticCandidateIncludingManualContext()
    {
        foreach (var day in new[] {24,25,26})
        foreach (var other in new[] {24,25,26}.Where(x=>x!=day))
        foreach (var status in new[] {PlanningAssignmentStatus.Manual,PlanningAssignmentStatus.Generated})
        foreach (var code in new[] {"10","R","R2","RN"})
        {
            var driver = new Driver {Id=Guid.NewGuid(),CompanyId=Guid.NewGuid()};
            var duty = new PlanningDuty {Id=Guid.NewGuid(),DutyNumber=code,StartTime=new TimeOnly(8,0),EndTime=new TimeOnly(16,0)};
            var date=new DateOnly(2026,12,day);
            var existing=new PlanningAssignment {DriverId=driver.Id,Date=new DateOnly(2026,12,other),Status=status,
                AssignmentType=PlanningAssignmentType.Duty,PlanningDuty=duty};
            var result=new PlanningEligibilityChecker().Evaluate(driver,duty,date,
                new PlanningWorkInterval(date.ToDateTime(new TimeOnly(8,0)),date.ToDateTime(new TimeOnly(16,0)),480,false),480,
                new[]{existing},Array.Empty<PlanningDriverAvailability>(),new PlanningGenerationOptions(),
                new DateOnly(2026,12,1),new DateOnly(2026,12,31));
            CollectionAssert.Contains(result.RejectionReasons,PlanningCandidateRejectionReason.ChristmasWorkdayLimit);
            Assert.AreEqual(status,existing.Status);
        }
    }

    [TestMethod]
    public void RestDayDoesNotConsumeChristmasWorkLimit()
    {
        var driver=new Driver {Id=Guid.NewGuid(),CompanyId=Guid.NewGuid()};
        var duty=new PlanningDuty {DutyNumber="10"};
        var existing=new PlanningAssignment {DriverId=driver.Id,Date=new DateOnly(2026,12,24),AssignmentType=PlanningAssignmentType.DayOff};
        var result=new PlanningEligibilityChecker().Evaluate(driver,duty,new DateOnly(2026,12,25),null,480,
            new[]{existing},Array.Empty<PlanningDriverAvailability>(),new PlanningGenerationOptions(),
            new DateOnly(2026,12,1),new DateOnly(2026,12,31));
        CollectionAssert.DoesNotContain(result.RejectionReasons,PlanningCandidateRejectionReason.ChristmasWorkdayLimit);
    }

    [TestMethod]
    public void NightPlannerCoversChristmasWithDifferentDriversAndPreservesManualWork()
    {
        var company=Guid.NewGuid();
        var drivers=Enumerable.Range(0,8).Select(x=>new Driver {Id=Guid.NewGuid(),CompanyId=company,LastName=$"Driver{x}"}).ToList();
        var duty=new PlanningDuty {Id=Guid.NewGuid(),CompanyId=company,DutyNumber="RN",StartTime=new TimeOnly(20,0),EndTime=new TimeOnly(6,0),WorkMinutes=480};
        var schedule=new PlanningSchedule {Id=Guid.NewGuid(),CompanyId=company,Year=2026,Month=12};
        var manual=new PlanningAssignment {Id=Guid.NewGuid(),DriverId=drivers[0].Id,Date=new DateOnly(2026,12,25),
            PlanningDuty=duty,Status=PlanningAssignmentStatus.Manual,AssignmentType=PlanningAssignmentType.Duty};
        var context=new List<PlanningAssignment>{manual};
        var planner=new PlanningTechnicalAssignmentPlanner(new PlanningCandidateEvaluator(new PlanningEligibilityChecker()),
            new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider()));
        var result=planner.PlanNightDuties(drivers,new[]{duty},
            new Dictionary<(int,int),PlanningSchedule>{{(2026,12),schedule}},context,
            Array.Empty<PlanningDriverAvailability>(),new DateOnly(2026,12,24),new DateOnly(2026,12,26),
            new PlanningGenerationOptions(),company,DateTime.UtcNow);
        foreach(var day in new[]{24,25,26})
            Assert.IsTrue(context.Any(x=>x.Date==new DateOnly(2026,12,day)), $"Missing RN on {day}");
        Assert.IsTrue(context.GroupBy(x=>x.DriverId).All(g=>g.Select(x=>x.Date).Distinct().Count()==1));
        Assert.AreEqual(PlanningAssignmentStatus.Manual,manual.Status);
        Assert.IsFalse(result.Assignments.Any(x=>x.DriverId==drivers[0].Id));
    }
}
