using Sigmap.Domain.Enums;

namespace Sigmap.Application.Abstractions;

/// <summary>Mirrors the frontend `LocatedDetection` (and proto `LocatedDetection`).</summary>
public class LocatedDetection
{
    public string Mac { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public string? Ssid { get; set; }
    public string? BtName { get; set; }
    public int SignalDbm { get; set; }
    public int Channel { get; set; }
    public Encryption Encryption { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
    public LocationFlag LocationFlag { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public string? VendorName { get; set; }
}

/// <summary>Mirrors the frontend `Heartbeat` (and proto `Heartbeat`).</summary>
public class Heartbeat
{
    public Guid DeviceId { get; set; }
    public DateTimeOffset At { get; set; }
    public string Status { get; set; } = "online";
    public int? CurrentChannel { get; set; }
    public int? BatteryPct { get; set; }
    public bool GpsFix { get; set; }
    public int DetectionsBuffered { get; set; }
    public int DetectionsSentTotal { get; set; }
}

/// <summary>
/// Mirrors the frontend `LiveEvent` oneof (`src/lib/domain.ts`) 1:1 — the same
/// shape flows over the in-memory bus and the gRPC-Web stream.
/// </summary>
public abstract record LiveEvent
{
    public required Guid SessionId { get; init; }

    public sealed record Detections : LiveEvent
    {
        public required string BatchId { get; init; }
        public required Guid DeviceId { get; init; }
        public required List<LocatedDetection> Items { get; init; }
    }

    public sealed record Device : LiveEvent
    {
        public required Heartbeat Heartbeat { get; init; }
    }

    public sealed record Config : LiveEvent
    {
        public required Guid DeviceId { get; init; }
        public required string PushId { get; init; }
        public required PushState Status { get; init; }
    }

    public sealed record Fleet : LiveEvent
    {
        public required List<Heartbeat> Devices { get; init; }
    }
}
