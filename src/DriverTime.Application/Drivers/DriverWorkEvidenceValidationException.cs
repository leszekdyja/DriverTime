namespace DriverTime.Application.Drivers;

public class DriverWorkEvidenceValidationException : Exception
{
    public DriverWorkEvidenceValidationException(IEnumerable<string> errors)
        : base("Nieprawidłowe dane ewidencji czasu pracy kierowcy.")
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyList<string> Errors { get; }
}
