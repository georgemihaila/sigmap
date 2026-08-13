using Sigmap.Domain.Enums;

namespace Sigmap.Domain.Entities;

/// <summary>Platform-level pairing request from a scanner wanting to join the fleet.</summary>
public class Pairing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string CapabilitiesJson { get; set; } = string.Empty;
    public PairingStatus Status { get; set; } = PairingStatus.Pending;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? SessionId { get; set; }
}
