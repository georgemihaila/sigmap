namespace Sigmap.Domain.Enums;

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
    Gps,
    Inferred,
    Unlocated,
}

public enum DetectionSource
{
    Scanner,
    WigleImport,
}

public enum DeviceType
{
    [EnumValue("AP")] Ap,
    [EnumValue("BLUETOOTH")] Bluetooth,
    [EnumValue("BT_LE")] BtLe,
    [EnumValue("CLIENT")] Client,
}

public enum Encryption
{
    Unspecified,
    Open,
    Wep,
    Wpa,
    Wpa2,
    Wpa3,
}

public enum ExportFormat
{
    [EnumValue("wigle_csv")] WigleCsv,
    [EnumValue("csv")] Csv,
    [EnumValue("geojson")] GeoJson,
}

public enum ExportStatus
{
    Queued,
    Running,
    Done,
    Failed,
}

public enum PairingStatus
{
    Pending,
    Approved,
    Rejected,
    Expired,
}
