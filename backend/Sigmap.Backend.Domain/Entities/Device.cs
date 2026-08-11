namespace Sigmap.Backend.Domain.Entities;

/// <summary>A physical scanner unit. Device type is the wire-level enum from the shared contracts.</summary>
public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>Platform string reported at pairing, e.g. "linux-x64" / "android-14".</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Reported capabilities, JSON-serialized from the pairing contract.</summary>
    public string CapabilitiesJson { get; set; } = "{}";

    public DeviceStatus Status { get; set; } = DeviceStatus.Offline;
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public string? LastKnownIp { get; set; }
    public DateTimeOffset PairedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<SessionDevice> Sessions { get; set; } = new List<SessionDevice>();
}
