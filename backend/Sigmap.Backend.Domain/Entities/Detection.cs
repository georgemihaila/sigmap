using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Domain.Entities;

/// <summary>A raw detection event with resolved-or-flagged location.</summary>
public class Detection
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public DetectionBatch? Batch { get; set; }
    public Guid SessionId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? SwarmId { get; set; }

    public DeviceType DeviceType { get; set; }
    public string Mac { get; set; } = string.Empty;
    public string? Ssid { get; set; }
    public string? BtName { get; set; }
    public string? BtUuid { get; set; }
    public int SignalDbm { get; set; }
    public int Channel { get; set; }
    public Encryption Encryption { get; set; } = Encryption.Unspecified;
    public DateTimeOffset DetectedAt { get; set; }

    public LocationFlag LocationFlag { get; set; } = LocationFlag.Unlocated;

    /// <summary>Null when unlocated. Not JSON-serialized (NTS types).</summary>
    [JsonIgnore]
    public Point? Geom { get; set; }

    public double? Latitude => Geom?.Y;
    public double? Longitude => Geom?.X;

    public DetectionSource Source { get; set; } = DetectionSource.Scanner;
}
