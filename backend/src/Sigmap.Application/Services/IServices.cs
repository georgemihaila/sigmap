using Sigmap.Application.Dtos;

namespace Sigmap.Application.Services;

public interface IAuthService
{
    Task<UserDto?> ValidateAsync(string username, string password, CancellationToken ct);
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct);
}

public interface ISessionService
{
    Task<List<SessionDto>> ListAsync(CancellationToken ct);
    Task<SessionDto?> GetAsync(Guid id, CancellationToken ct);
    Task<SessionDto> CreateAsync(SessionInputDto input, CancellationToken ct);
    Task<SessionDto?> UpdateAsync(Guid id, SessionInputDto input, CancellationToken ct);
    Task<SessionDto?> ArchiveAsync(Guid id, CancellationToken ct);
    Task<SessionStatsDto> GetStatsAsync(Guid sessionId, CancellationToken ct);
    Task<List<SwarmDto>> ListSwarmsAsync(Guid sessionId, CancellationToken ct);
    Task<CoverageDto> GetCoverageAsync(Guid sessionId, int limit, CancellationToken ct);
    Task<LiveSessionStateDto> GetLiveSnapshotAsync(Guid sessionId, CancellationToken ct);
}

public interface IFleetService
{
    Task<List<FleetDeviceDto>> ListAsync(Guid? sessionId, CancellationToken ct);
}

public interface IConfigService
{
    Task<DeviceConfigDto?> GetDeviceConfigAsync(Guid sessionId, Guid deviceId, CancellationToken ct);
    Task<PushStatusDto> GetPushStatusAsync(Guid sessionId, Guid deviceId, CancellationToken ct);
    Task<ConfigPushResult> ApplyToDeviceAsync(Guid sessionId, Guid deviceId, string configJson, Guid? presetId, CancellationToken ct);
    Task<List<ConfigPushResult>> ApplyToManyAsync(Guid sessionId, string configJson, List<Guid>? deviceIds, CancellationToken ct);
    Task<List<ConfigDiffDto>> PreviewSessionAsync(Guid sessionId, string configJson, CancellationToken ct);
    Task<List<ConfigDiffDto>> PreviewPresetAsync(Guid sessionId, Guid presetId, CancellationToken ct);
    Task<List<ConfigPushResult>> ApplyPresetAsync(Guid sessionId, Guid presetId, List<Guid>? deviceIds, CancellationToken ct);
}

public interface IPresetService
{
    Task<List<ScanPresetDto>> ListAsync(CancellationToken ct);
    Task<ScanPresetDto> CreateAsync(PresetInputDto input, Guid ownerId, CancellationToken ct);
    Task<ScanPresetDto?> UpdateAsync(Guid id, PresetInputDto input, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}

public interface IDetectedService
{
    Task<DetectedDevicePageDto> ListAsync(DetectedQuery query, CancellationToken ct);
    Task<DetectedDeviceDto?> GetByMacAsync(string mac, CancellationToken ct);
    Task<List<DetectedDeviceDto>> ListLocatedAsync(CancellationToken ct);
    Task<List<SignalPointDto>> GetSignalSeriesAsync(string mac, CancellationToken ct);
}

public interface IExportService
{
    Task<List<ExportRecordDto>> ListAsync(CancellationToken ct);
    Task<ExportCreatedDto> CreateAsync(ExportInputDto input, Guid ownerId, CancellationToken ct);
    Task<(byte[] Content, string FileName)?> DownloadAsync(Guid id, CancellationToken ct);
}

public interface IPairingService
{
    Task<List<PairingRequestDto>> ListPendingAsync(CancellationToken ct);
    Task<bool> ApproveAsync(Guid deviceId, Guid? sessionId, CancellationToken ct);
    Task<bool> RejectAsync(Guid deviceId, CancellationToken ct);
    Task<PairingQrDto> GetQrAsync(CancellationToken ct);
}
public interface ISettingsService
{
    Task<WigleSettingsDto> GetWigleAsync(CancellationToken ct);
    Task<WigleSettingsDto> UpdateWigleAsync(WigleSettingsInputDto input, CancellationToken ct);
}
