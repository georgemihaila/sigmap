using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;

namespace Sigmap.Backend.Domain.Entities;

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }

    /// <summary>Optional geographic bounding area (SRID 4326). Not JSON-serialized.</summary>
    [JsonIgnore]
    public Polygon? BoundingArea { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Planned;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Swarm> Swarms { get; set; } = new List<Swarm>();
    public ICollection<SessionDevice> Devices { get; set; } = new List<SessionDevice>();
}
