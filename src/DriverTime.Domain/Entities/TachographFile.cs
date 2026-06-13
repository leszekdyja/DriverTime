namespace DriverTime.Domain.Entities;

public class TachographFile
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public string FileType { get; set; } = null!;

    public DateTime UploadedAtUtc { get; set; }

    public long FileSize { get; set; }

    public string ParserStatus { get; set; } = null!;
}