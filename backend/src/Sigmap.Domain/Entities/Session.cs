using Sigmap.Domain.Enums;

namespace Sigmap.Domain.Entities;

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Planned;
    public double? LatMin { get; set; }
    public double? LonMin { get; set; }
    public double? LatMax { get; set; }
    public double? LonMax { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
