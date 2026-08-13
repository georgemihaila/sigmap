using System.Text.Json;
using Sigmap.Application.Dtos;
using Sigmap.Domain.Entities;

namespace Sigmap.Infrastructure.Services;

public static class Mappers
{
    private static readonly JsonSerializerOptions CapJson = new() { PropertyNameCaseInsensitive = true };

    public static DeviceCapabilitiesDto Capabilities(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new DeviceCapabilitiesDto(false, false, false, false, "");
        try
        {
            var c = JsonSerializer.Deserialize<DeviceCapabilitiesDto>(json, CapJson);
            return c ?? new DeviceCapabilitiesDto(false, false, false, false, "");
        }
        catch (JsonException)
        {
            return new DeviceCapabilitiesDto(false, false, false, false, "");
        }
    }

    public static string CapabilitiesJson(DeviceCapabilities c) => JsonSerializer.Serialize(c);

    public static string CapabilitiesJson(DeviceCapabilitiesDto c) => JsonSerializer.Serialize(c);

    public static UserDto ToDto(User u) => new(u.Id, u.Username, u.Role);

    public static SessionDto ToDto(Session s) => new(
        s.Id,
        s.Name,
        s.Description,
        s.StartsAt,
        s.EndsAt,
        s.Status,
        s.LatMin is not null && s.LonMin is not null && s.LatMax is not null && s.LonMax is not null
            ? new BoundingAreaDto(s.LatMin.Value, s.LonMin.Value, s.LatMax.Value, s.LonMax.Value)
            : null,
        s.CreatedAt,
        s.UpdatedAt);

    public static DeviceDto ToDto(Device d) => new(
        d.Id,
        d.Name,
        d.Platform,
        Capabilities(d.CapabilitiesJson),
        d.Status,
        d.LastHeartbeatAt,
        d.LastKnownIp,
        d.PairedAt);

    public static DeviceConfigDto ToDto(SessionDevice sd) => new(
        sd.SessionId,
        sd.DeviceId,
        sd.ConfigJson,
        sd.ConfigRev,
        sd.PresetId,
        sd.ConfigSource,
        sd.PushState,
        sd.LastPushId,
        sd.ConfigUpdatedAt);

    public static ScanPresetDto ToDto(Preset p) => new(
        p.Id,
        p.Name,
        p.Description,
        p.OwnerId,
        p.ConfigJson,
        p.IsBuiltin,
        p.CreatedAt,
        p.UpdatedAt);

    public static DetectedDeviceDto ToDto(DetectedDevice d) => new(
        d.Id,
        d.Mac,
        d.MacNormalized,
        d.VendorOui,
        d.VendorName,
        d.DeviceType,
        d.SsidLatest,
        d.BtNameLatest,
        d.ChannelLatest,
        d.FirstSeenAt,
        d.LastSeenAt,
        d.Latitude,
        d.Longitude,
        d.DetectionCount);

    public static ExportRecordDto ToDto(Export e) => new(
        e.Id,
        e.SessionId,
        e.SessionName,
        e.Format,
        e.Status,
        e.FileName,
        e.CreatedAt,
        e.OwnerId,
        e.SizeBytes,
        e.RowCount);

    public static PairingRequestDto ToDto(Pairing p) => new(
        p.Id,
        p.DeviceId,
        p.DeviceName,
        p.Platform,
        Capabilities(p.CapabilitiesJson),
        p.Status,
        p.RequestedAt,
        p.SessionId);

    public static WigleSettingsDto ToDto(AppSetting s) => new(
        s.WigleApiName,
        s.WigleApiKeySet,
        s.WigleUsername,
        s.WiglePasswordSet);
}
