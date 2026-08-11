using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Domain.Entities;

/// <summary>Aggregate head of all observations of a unique MAC — the WiGLE-equivalent table.</summary>
public class DetectedDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Mac { get; set; } = string.Empty;
    public string MacNormalized { get; set; } = string.Empty;

    /// <summary>First three MAC bytes as hex, used for vendor lookup.</summary>
    public string? VendorOui { get; set; }
    public string? VendorName { get; set; }

    public DeviceType DeviceType { get; set; }
    public string? SsidLatest { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Most recent known position (SRID 4326). Not JSON-serialized.</summary>
    [JsonIgnore]
    public Point? Geom { get; set; }

    public double? Latitude => Geom?.Y;
    public double? Longitude => Geom?.X;
}
