namespace DriverTime.Domain.Entities;

public class Driver
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string CardNumber { get; set; } = string.Empty;

    public DateTime? CardExpiryDate { get; set; }

    public string CardIssuingCountry { get; set; } = string.Empty;

    public ICollection<DddFile> DddFiles { get; set; } = new List<DddFile>();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
