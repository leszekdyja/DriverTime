using DriverTime.Domain.Entities;

namespace DriverTime.Application.Planning;

public static class PlanningEntryClassifier
{
    public static PlanningEntryClassification Classify(PlanningAssignment assignment) =>
        Classify(assignment.AssignmentType, assignment.PlanningDuty?.DutyNumber);

    public static PlanningEntryClassification Classify(PlanningDuty duty) =>
        Classify(PlanningAssignmentType.Duty, duty.DutyNumber);

    public static PlanningEntryClassification Classify(PlanningAssignmentType type, string? dutyNumber = null)
    {
        var code = NormalizeCode(dutyNumber);
        if (!string.IsNullOrWhiteSpace(code))
        {
            var byCode = ClassifyCode(code);
            if (byCode is not null)
            {
                return byCode;
            }
        }

        return type switch
        {
            PlanningAssignmentType.Duty => Work(PlanningEntryKind.Duty, code, requiresDuty: true, generatedTechnical: false),
            PlanningAssignmentType.Training => Work(PlanningEntryKind.Duty, code, requiresDuty: false, generatedTechnical: false),
            PlanningAssignmentType.DayOff => Rest(PlanningEntryKind.DayOff, "W"),
            PlanningAssignmentType.Vacation => CreditedAbsence(PlanningEntryKind.PaidVacation, "UW"),
            PlanningAssignmentType.SickLeave => Absence(PlanningEntryKind.SickLeave, "CH"),
            PlanningAssignmentType.Other => Absence(PlanningEntryKind.Other, code),
            _ => Absence(PlanningEntryKind.Other, code)
        };
    }

    private static PlanningEntryClassification? ClassifyCode(string code) => code switch
    {
        "RN" => Work(PlanningEntryKind.NightDuty, code, requiresDuty: true, generatedTechnical: true),
        "R" => Work(PlanningEntryKind.ReserveFirstShift, code, requiresDuty: true, generatedTechnical: true),
        "R2" => Work(PlanningEntryKind.ReserveSecondShift, code, requiresDuty: true, generatedTechnical: true),
        "WG" => Rest(PlanningEntryKind.WeeklyDayOff, code, generatedTechnical: true),
        "W" => Rest(PlanningEntryKind.DayOff, code, generatedTechnical: true),
        "UW" => CreditedAbsence(PlanningEntryKind.PaidVacation, code),
        "UO" => Absence(PlanningEntryKind.UnpaidVacation, code),
        "UB" => Absence(PlanningEntryKind.OtherLeave, code),
        "CH" => Absence(PlanningEntryKind.SickLeave, code),
        "NAJEM" => Work(PlanningEntryKind.RentalFirstShift, code, requiresDuty: true, generatedTechnical: false),
        "NAJEM2" => Work(PlanningEntryKind.RentalSecondShift, code, requiresDuty: true, generatedTechnical: false),
        "REZERWACJA" => Absence(PlanningEntryKind.Reservation, code),
        _ => null
    };

    private static PlanningEntryClassification Work(
        PlanningEntryKind kind,
        string code,
        bool requiresDuty,
        bool generatedTechnical) => new()
    {
        Kind = kind,
        Code = code,
        IsRealWork = true,
        IsTimedWork = true,
        CountsTowardWorkMinutes = true,
        CountsTowardMonthlyNorm = true,
        CountsAsConsecutiveWorkDay = true,
        RequiresDuty = requiresDuty,
        IsGeneratedTechnicalEntry = generatedTechnical
    };

    private static PlanningEntryClassification Rest(
        PlanningEntryKind kind,
        string code,
        bool generatedTechnical = false) => new()
    {
        Kind = kind,
        Code = code,
        IsRestDay = true,
        IsGeneratedTechnicalEntry = generatedTechnical
    };

    private static PlanningEntryClassification CreditedAbsence(PlanningEntryKind kind, string code) => new()
    {
        Kind = kind,
        Code = code,
        IsRestDay = true,
        CountsTowardMonthlyNorm = true,
        IsCreditedAbsence = true
    };

    private static PlanningEntryClassification Absence(PlanningEntryKind kind, string code) => new()
    {
        Kind = kind,
        Code = code,
        IsRestDay = true
    };

    private static string NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? string.Empty
            : code.Trim().ToUpperInvariant().Replace(" ", string.Empty);
}
