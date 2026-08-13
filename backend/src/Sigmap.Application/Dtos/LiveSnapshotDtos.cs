using Sigmap.Domain.Enums;

namespace Sigmap.Application.Dtos;

public record LiveMapPointDto(
    string Mac,
    double Lat,
    double Lon,
    DeviceType Type,
    string? Ssid,
    int SignalDbm,
    string LastSeen);

public record LiveHeartbeatDto(
    string DeviceId,
    string At,
    string Status,
    int? CurrentChannel,
    int? BatteryPct,
    bool GpsFix,
    int DetectionsBuffered,
    int DetectionsSentTotal);

public record LiveSessionStateDto(
    Guid SessionId,
    Dictionary<string, LiveMapPointDto> Points,
    List<object> Unlocated,
    int BatchCount,
    string? LastBatchAt,
    Dictionary<string, LiveHeartbeatDto> Devices);
