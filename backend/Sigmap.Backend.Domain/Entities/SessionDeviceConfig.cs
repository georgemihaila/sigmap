namespace Sigmap.Backend.Domain.Entities;

/// <summary>Per-(session, device) scan config plus config-push state tracking.</summary>
public class SessionDeviceConfig
{
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }

    /// <summary>Current ScanConfig (JSON). Null until first push.</summary>
    public string? ConfigJson { get; set; }
    public int ConfigRev { get; set; }

    public Guid? PresetId { get; set; }
    public ScanPreset? Preset { get; set; }
    public ConfigSource Source { get; set; } = ConfigSource.Custom;

    /// <summary>pending until the device acks; drives crash-resumable propagation.</summary>
    public PushState PushState { get; set; } = PushState.Acked;
    public Guid? LastPushId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
