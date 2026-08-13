using Sigmap.Domain.Enums;

namespace Sigmap.Domain.Entities;

/// <summary>Radio/GPS capabilities a scanner reports when pairing.</summary>
public class DeviceCapabilities
{
    public bool HasWifiMonitor { get; set; }
    public bool HasBluetooth { get; set; }
    public bool HasGps { get; set; }
    public bool HasBattery { get; set; }
    public string Platform { get; set; } = string.Empty;
}

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    /// <summary>Serialized <see cref="DeviceCapabilities"/>.</summary>
    public string CapabilitiesJson { get; set; } = string.Empty;
    public DeviceStatus Status { get; set; } = DeviceStatus.Pending;
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public string? LastKnownIp { get; set; }
    public DateTimeOffset PairedAt { get; set; } = DateTimeOffset.UtcNow;
}
