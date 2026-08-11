namespace Sigmap.Backend.Domain.Entities;

/// <summary>Membership of a device in a session, optionally in a swarm.</summary>
public class SessionDevice
{
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }
    public Guid? SwarmId { get; set; }
    public Swarm? Swarm { get; set; }
    public string Role { get; set; } = "scanner";
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}
