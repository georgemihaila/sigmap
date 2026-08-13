using Sigmap.Domain.Enums;

namespace Sigmap.Domain.Entities;

public class Export
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SessionId { get; set; }
    public string? SessionName { get; set; }
    public ExportFormat Format { get; set; }
    public ExportStatus Status { get; set; } = ExportStatus.Queued;
    public string? FileName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid OwnerId { get; set; }
    public long? SizeBytes { get; set; }
    public long? RowCount { get; set; }
}
