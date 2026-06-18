namespace DriverTime.Domain.Entities;

public class CardReadSession
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public Guid? UserId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string ReaderName { get; set; } = string.Empty;

    public string DriverCardNumber { get; set; } = string.Empty;

    public Guid? DddFileId { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? FailedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
