namespace DriverTime.Application.Planning;

public class PlanningDriverWorkloadSummary
{
    public int RealWorkMinutes { get; init; }

    public int CreditedAbsenceMinutes { get; init; }

    public int MonthlyNormMinutes => RealWorkMinutes + CreditedAbsenceMinutes;
}
