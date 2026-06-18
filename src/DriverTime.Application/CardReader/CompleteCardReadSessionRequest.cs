namespace DriverTime.Application.CardReader;

public class CompleteCardReadSessionRequest
{
    public string? DriverCardNumber { get; set; }

    public Guid? DddFileId { get; set; }

    public string? Notes { get; set; }
}
