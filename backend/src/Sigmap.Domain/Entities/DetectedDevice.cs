using Sigmap.Domain.Enums;

namespace Sigmap.Domain.Entities;

/// <summary>Aggregate head per normalized MAC (the WiGLE-equivalent table).</summary>
public class DetectedDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Mac { get; set; } = string.Empty;
    public string MacNormalized { get; set; } = string.Empty;
    public string? VendorOui { get; set; }
    public string? VendorName { get; set; }
    public DeviceType DeviceType { get; set; }
    public string? SsidLatest { get; set; }
    public string? BtNameLatest { get; set; }
    public int? ChannelLatest { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int DetectionCount { get; set; }
}
