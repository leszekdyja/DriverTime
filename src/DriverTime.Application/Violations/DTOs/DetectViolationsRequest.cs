namespace DriverTime.Application.Violations.DTOs;

public class DetectViolationsRequest
{
    public Guid DriverId { get; set; }

    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }
}
