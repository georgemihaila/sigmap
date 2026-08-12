using Sigmap.Contracts.Geo;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Application.Configuration;

public sealed record SessionConfigDto(
    string? ConfigJson,
    Guid? PresetId,
    string Source,
    string PushState,
    int ConfigRev,
    Guid? LastPushId,
    DateTimeOffset UpdatedAt);

public sealed record SessionConfigPushResult(
    Guid SessionId,
    Guid DeviceId,
    string PushId,
    int ConfigRev,
    string PushState);

/// <summary>Reads and updates a (session, device) scan config, tracking push state.</summary>
public interface ISessionConfigService
{
    Task<SessionConfigDto?> GetAsync(Guid sessionId, Guid deviceId, CancellationToken ct);
    Task<SessionConfigDto?> GetLatestForDeviceAsync(Guid deviceId, CancellationToken ct);
    Task<SessionConfigPushResult> ApplyAsync(
        Guid sessionId, Guid deviceId, ScanConfig config, Guid? presetId, CancellationToken ct);
}
