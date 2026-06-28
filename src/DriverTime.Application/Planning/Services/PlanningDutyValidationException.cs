namespace DriverTime.Application.Planning.Services;

public class PlanningDutyValidationException : Exception
{
    public PlanningDutyValidationException(IReadOnlyList<string> errors)
        : base("Planning duty validation failed.")
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
