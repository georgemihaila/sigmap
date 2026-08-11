namespace Sigmap.Backend.Domain;

public enum UserRole
{
    Viewer,
    Operator,
}

public enum SessionStatus
{
    Planned,
    Active,
    Archived,
}

public enum DeviceStatus
{
    /// <summary>Registered but awaiting operator approval.</summary>
    Pending,
    Offline,
    Online,
    Error,
}

public enum PushState
{
    Pending,
    Acked,
    Failed,
}

public enum ConfigSource
{
    Preset,
    Custom,
}

public enum LocationFlag
{
    /// <summary>Resolved from the device's own GPS track.</summary>
    Gps,
    /// <summary>Time-interpolated from a GPS-equipped swarm member.</summary>
    Inferred,
    /// <summary>No GPS evidence within the allowed window.</summary>
    Unlocated,
}

public enum DetectionSource
{
    Scanner,
    WigleImport,
}

public enum ExportFormat
{
    WigleCsv,
    Csv,
    GeoJson,
}

public enum ExportStatus
{
    Queued,
    Running,
    Done,
    Failed,
}

public enum WatchlistAction
{
    Highlight,
    Exclude,
}
