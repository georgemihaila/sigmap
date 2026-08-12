using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sigmap.Backend.Application.Configuration;
using Sigmap.Backend.Domain;
using Sigmap.Backend.Domain.Entities;
using Sigmap.Backend.Infrastructure.Persistence;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Infrastructure.Configuration;

public sealed class SessionConfigService : ISessionConfigService
{
    private readonly SigmapDbContext _db;
    private readonly IConfigPushPublisher _publisher;
    private readonly ILogger<SessionConfigService> _log;

    public SessionConfigService(SigmapDbContext db, IConfigPushPublisher publisher, ILogger<SessionConfigService> log)
    {
        _db = db;
        _publisher = publisher;
        _log = log;
    }

    public async Task<SessionConfigDto?> GetAsync(Guid sessionId, Guid deviceId, CancellationToken ct)
    {
        var row = await _db.SessionDeviceConfigs
            .FirstOrDefaultAsync(c => c.SessionId == sessionId && c.DeviceId == deviceId, ct);
        if (row is null)
            return null;

        return new SessionConfigDto(
            row.ConfigJson,
            row.PresetId,
            row.Source.ToString(),
            row.PushState.ToString(),
            row.ConfigRev,
            row.LastPushId,
            row.UpdatedAt);
    }

    /// <summary>Returns the most recently updated config for a device across all
    /// sessions, used by the agent to pull its current config on startup.</summary>
    public async Task<SessionConfigDto?> GetLatestForDeviceAsync(Guid deviceId, CancellationToken ct)
    {
        var row = await _db.SessionDeviceConfigs
            .Where(c => c.DeviceId == deviceId)
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        if (row is null)
            return null;

        return new SessionConfigDto(
            row.ConfigJson,
            row.PresetId,
            row.Source.ToString(),
            row.PushState.ToString(),
            row.ConfigRev,
            row.LastPushId,
            row.UpdatedAt);
    }

    public async Task<SessionConfigPushResult> ApplyAsync(
        Guid sessionId, Guid deviceId, ScanConfig config, Guid? presetId, CancellationToken ct)
    {
        var row = await _db.SessionDeviceConfigs
            .FirstOrDefaultAsync(c => c.SessionId == sessionId && c.DeviceId == deviceId, ct);
        if (row is null)
        {
            row = new SessionDeviceConfig { SessionId = sessionId, DeviceId = deviceId };
            _db.SessionDeviceConfigs.Add(row);
        }

        row.ConfigJson = ScanConfigFactory.ToJson(config);
        row.ConfigRev++;
        row.PresetId = presetId;
        row.Source = presetId.HasValue ? ConfigSource.Preset : ConfigSource.Custom;
        row.PushState = PushState.Pending;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var push = await _publisher.PushAsync(sessionId, deviceId, config, presetId, ct);
        row.LastPushId = Guid.Parse(push.PushId);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Pushed config rev {Rev} to device {DeviceId} (session {SessionId}, push {PushId})",
            row.ConfigRev, deviceId, sessionId, push.PushId);

        return new SessionConfigPushResult(sessionId, deviceId, push.PushId, row.ConfigRev, row.PushState.ToString());
    }
}
