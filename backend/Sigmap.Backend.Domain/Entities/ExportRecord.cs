namespace Sigmap.Backend.Domain.Entities;

public class ExportRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SessionId { get; set; }
    public Guid OwnerId { get; set; }
    public ExportFormat Format { get; set; }
    public ExportStatus Status { get; set; } = ExportStatus.Queued;
    public string? FilePath { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
