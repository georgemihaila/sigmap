using Sigmap.Domain.Enums;

namespace Sigmap.Application.Dtos;

public record DeviceCapabilitiesDto(
    bool HasWifiMonitor,
    bool HasBluetooth,
    bool HasGps,
    bool HasBattery,
    string Platform);

public record DeviceDto(
    Guid Id,
    string Name,
    string Platform,
    DeviceCapabilitiesDto Capabilities,
    DeviceStatus Status,
    DateTimeOffset? LastHeartbeatAt,
    string? LastKnownIp,
    DateTimeOffset PairedAt);

public record DeviceConfigDto(
    Guid SessionId,
    Guid DeviceId,
    string? ConfigJson,
    int ConfigRev,
    Guid? PresetId,
    ConfigSource Source,
    PushState PushState,
    string? LastPushId,
    DateTimeOffset UpdatedAt);

public record ConfigPushResult(
    Guid SessionId,
    Guid DeviceId,
    string PushId,
    int ConfigRev,
    PushState PushState);

public record FleetDeviceDto(
    DeviceDto Device,
    Guid? SessionId,
    string? SessionName,
    Guid? SwarmId,
    string? SwarmName,
    string? Role,
    bool Drift,
    Guid? PresetId,
    string? PresetName,
    DeviceConfigDto? Config);

public record PushStatusDto(PushState PushState, string? LastPushId, int ConfigRev);
