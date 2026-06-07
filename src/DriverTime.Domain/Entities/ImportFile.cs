using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class ImportFile : BaseEntity
{
    public Guid CompanyId { get; set; }

    public Company? Company { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DriverActivity> Activities { get; set; } = new List<DriverActivity>();
}
