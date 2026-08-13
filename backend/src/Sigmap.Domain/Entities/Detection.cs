using Sigmap.Domain.Enums;

namespace Sigmap.Domain.Entities;

/// <summary>Append-only raw detection row (a batch of these comes from one scanner).</summary>
public class Detection
{
    public long Id { get; set; }
    public string BatchId { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? SwarmId { get; set; }
    public DeviceType DeviceType { get; set; }
    public string Mac { get; set; } = string.Empty;
    public string MacNormalized { get; set; } = string.Empty;
    public string? Ssid { get; set; }
    public string? BtName { get; set; }
    public int Channel { get; set; }
    public int SignalDbm { get; set; }
    public Encryption Encryption { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
    public LocationFlag LocationFlag { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public DetectionSource Source { get; set; }
}
