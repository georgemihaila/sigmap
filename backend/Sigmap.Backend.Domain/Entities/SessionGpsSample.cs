using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;

namespace Sigmap.Backend.Domain.Entities;

/// <summary>GPS sample recorded by a device during a session — drives heatmap/coverage.</summary>
public class SessionGpsSample
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid DeviceId { get; set; }
    [JsonIgnore]
    public Point Geom { get; set; } = new(0, 0) { SRID = 4326 };
    public DateTimeOffset At { get; set; }
}
