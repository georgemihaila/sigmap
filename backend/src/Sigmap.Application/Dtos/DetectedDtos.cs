using Sigmap.Domain.Enums;

namespace Sigmap.Application.Dtos;

public record DetectedDeviceDto(
    Guid Id,
    string Mac,
    string MacNormalized,
    string? VendorOui,
    string? VendorName,
    DeviceType DeviceType,
    string? SsidLatest,
    string? BtNameLatest,
    int? ChannelLatest,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    double? Latitude,
    double? Longitude,
    int DetectionCount);

public record DetectedDevicePageDto(
    List<DetectedDeviceDto> Items,
    string? NextCursor,
    int Count);

public record SignalPointDto(
    DateTimeOffset At,
    int SignalDbm,
    int Channel,
    LocationFlag LocationFlag,
    double? Lat,
    double? Lon);

public record DetectedQuery(string? Cursor, int Limit, string? DeviceType, string? Search);
