using DriverTime.Application.Planning;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;

namespace DriverTime.Infrastructure.Services;

public class PlanningAssignmentValidationService : IPlanningAssignmentValidationService
{
    public void ValidateManualAssignment(PlanningAssignmentValidationRequest request)
    {
        var errors = new List<string>();
        var driver = request.Driver;
        var duty = request.PlanningDuty;

        if (driver.CompanyId != request.CompanyId)
        {
            errors.Add("Kierowca nie należy do bieżącej firmy.");
        }

        if (duty.CompanyId != request.CompanyId)
        {
            errors.Add("Służba nie należy do bieżącej firmy.");
        }

        var otherAssignments = request.ExistingAssignments
            .Where(x => x.Id != request.AssignmentId)
            .ToList();

        if (otherAssignments.Any(x => x.DriverId == driver.Id && x.Date == request.Date))
        {
            errors.Add("Kierowca ma już wpis w wybranym dniu.");
        }

        AddAvailabilityErrors(errors, driver.Id, request.Date, request.Availabilities);
        AddForbiddenDutyErrors(errors, request.CompanyId, driver, duty, request.Date, request.AssignmentRules);
        AddTimeAndRestErrors(errors, driver.Id, duty, request.Date, otherAssignments, request.MinDailyRestMinutes);

        if (errors.Count > 0)
        {
            throw new PlanningDutyValidationException(errors.Distinct().ToList());
        }
    }

    private static void AddAvailabilityErrors(
        ICollection<string> errors,
        Guid driverId,
        DateOnly date,
        IEnumerable<PlanningDriverAvailability> availabilities)
    {
        foreach (var availability in availabilities.Where(x => x.DriverId == driverId && x.DateFrom <= date && x.DateTo >= date))
        {
            errors.Add(availability.Type switch
            {
                PlanningDriverAvailabilityType.Vacation => "Kierowca ma urlop w wybranym dniu.",
                PlanningDriverAvailabilityType.SickLeave => "Kierowca jest na chorobowym w wybranym dniu.",
                PlanningDriverAvailabilityType.DayOff => "Kierowca ma dzień wolny w wybranym dniu.",
                _ => "Kierowca jest oznaczony jako niedostępny w wybranym dniu."
            });
        }
    }

    private static void AddForbiddenDutyErrors(
        ICollection<string> errors,
        Guid companyId,
        Driver driver,
        PlanningDuty duty,
        DateOnly date,
        IEnumerable<PlanningAssignmentRule> rules)
    {
        var constraint = PlanningAssignmentConstraintEvaluator.Evaluate(companyId, driver, duty, date, rules);
        if (constraint.IsForbidden)
        {
            var dutyNumber = string.IsNullOrWhiteSpace(duty.DutyNumber) ? duty.Id.ToString() : duty.DutyNumber;
            errors.Add($"Kierowca ma zakaz wykonywania służby {dutyNumber}.");
        }
    }

    private static void AddTimeAndRestErrors(
        ICollection<string> errors,
        Guid driverId,
        PlanningDuty duty,
        DateOnly date,
        IEnumerable<PlanningAssignment> assignments,
        int minDailyRestMinutes)
    {
        var classification = PlanningEntryClassifier.Classify(duty);
        if (!classification.IsTimedWork)
        {
            return;
        }

        if (!PlanningWorkInterval.TryCreate(date, duty, out var candidateInterval))
        {
            errors.Add("Służba nie ma poprawnej godziny rozpoczęcia albo zakończenia.");
            return;
        }

        var intervals = assignments
            .Where(x => x.DriverId == driverId)
            .Where(PlanningWorkloadCalculator.IsWorkAssignment)
            .Select(x => PlanningWorkloadCalculator.ResolveAssignmentInterval(x))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .OrderBy(x => x.Start)
            .ToList();

        if (intervals.Any(x => candidateInterval.Start < x.End && candidateInterval.End > x.Start))
        {
            errors.Add("Kierowca ma już przydział w tym czasie.");
        }

        var previous = intervals
            .Where(x => x.End <= candidateInterval.Start)
            .OrderByDescending(x => x.End)
            .FirstOrDefault();
        if (previous != default)
        {
            var rest = (candidateInterval.Start - previous.End).TotalMinutes;
            if (rest < minDailyRestMinutes)
            {
                errors.Add("Po poprzedniej służbie nie został zachowany wymagany odpoczynek.");
            }
        }

        var next = intervals
            .Where(x => x.Start >= candidateInterval.End)
            .OrderBy(x => x.Start)
            .FirstOrDefault();
        if (next != default)
        {
            var rest = (next.Start - candidateInterval.End).TotalMinutes;
            if (rest < minDailyRestMinutes)
            {
                errors.Add("Przed następną służbą nie zostałby zachowany wymagany odpoczynek.");
            }
        }
    }
}
