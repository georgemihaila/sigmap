using Sigmap.Domain.Enums;

namespace Sigmap.Domain.Entities;

/// <summary>Session membership + the device's live scan-config target state.</summary>
public class SessionDevice
{
    public Guid SessionId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? SwarmId { get; set; }
    public string Role { get; set; } = "scout";
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? ConfigJson { get; set; }
    public int ConfigRev { get; set; }
    public Guid? PresetId { get; set; }
    public ConfigSource ConfigSource { get; set; } = ConfigSource.Custom;
    public PushState PushState { get; set; } = PushState.Acked;
    public string? LastPushId { get; set; }
    public DateTimeOffset ConfigUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
