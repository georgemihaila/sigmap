using Microsoft.EntityFrameworkCore;
using Sigmap.Application.Abstractions;
using Sigmap.Application.Dtos;
using Sigmap.Application.Services;
using Sigmap.Domain.Entities;
using Sigmap.Domain.Enums;
using Sigmap.Domain.ScanConfig;
using Sigmap.Infrastructure.Persistence;
using Sigmap.Infrastructure.Simulation;

namespace Sigmap.Infrastructure.Services;

public sealed class ConfigService(
    SigmapDbContext db,
    ILiveEventBus liveBus,
    IConfigPushQueue pushQueue) : IConfigService
{
    public Task<DeviceConfigDto?> GetDeviceConfigAsync(Guid sessionId, Guid deviceId, CancellationToken ct) =>
        db.SessionDevices.AsNoTracking()
            .Where(sd => sd.SessionId == sessionId && sd.DeviceId == deviceId)
            .Select(sd => Mappers.ToDto(sd))
            .FirstOrDefaultAsync(ct);

    public async Task<PushStatusDto> GetPushStatusAsync(Guid sessionId, Guid deviceId, CancellationToken ct)
    {
        var sd = await db.SessionDevices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SessionId == sessionId && x.DeviceId == deviceId, ct);
        return new PushStatusDto(sd?.PushState ?? PushState.Acked, sd?.LastPushId, sd?.ConfigRev ?? 0);
    }

    public async Task<ConfigPushResult> ApplyToDeviceAsync(Guid sessionId, Guid deviceId, string configJson, Guid? presetId, CancellationToken ct)
    {
        var sd = await db.SessionDevices
            .FirstOrDefaultAsync(x => x.SessionId == sessionId && x.DeviceId == deviceId, ct)
            ?? throw new KeyNotFoundException($"Device {deviceId} is not a member of session {sessionId}");

        sd.ConfigJson = configJson;
        sd.PresetId = presetId;
        sd.ConfigSource = presetId is null ? ConfigSource.Custom : ConfigSource.Preset;
        sd.PushState = PushState.Pending;
        sd.LastPushId = Guid.NewGuid().ToString("N");
        sd.ConfigUpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var pushId = sd.LastPushId!;
        liveBus.Publish(new LiveEvent.Config
        {
            SessionId = sessionId,
            DeviceId = deviceId,
            PushId = pushId,
            Status = PushState.Pending,
        });
        pushQueue.Enqueue(new PendingPush(sessionId, deviceId, pushId, sd.ConfigUpdatedAt));

        return new ConfigPushResult(sessionId, deviceId, pushId, sd.ConfigRev, PushState.Pending);
    }

    public async Task<List<ConfigPushResult>> ApplyToManyAsync(Guid sessionId, string configJson, List<Guid>? deviceIds, CancellationToken ct)
    {
        var members = await db.SessionDevices.Where(sd => sd.SessionId == sessionId).ToListAsync(ct);
        var targets = deviceIds is { Count: > 0 }
            ? members.Where(m => deviceIds.Contains(m.DeviceId)).ToList()
            : members;
        if (targets.Count == 0) return [];

        var results = new List<ConfigPushResult>(targets.Count);
        foreach (var target in targets)
        {
            results.Add(await ApplyToDeviceAsync(sessionId, target.DeviceId, configJson, null, ct));
        }
        return results;
    }

    public async Task<List<ConfigDiffDto>> PreviewSessionAsync(Guid sessionId, string configJson, CancellationToken ct)
    {
        var target = ScanConfigParser.Parse(configJson);
        var members = await db.SessionDevices.AsNoTracking().Where(sd => sd.SessionId == sessionId).ToListAsync(ct);
        var deviceNames = await db.Devices.AsNoTracking()
            .Where(d => members.Select(m => m.DeviceId).Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        return members.Select(sd =>
        {
            var current = ScanConfigParser.Parse(sd.ConfigJson);
            var changes = ScanConfigDiffer.Diff(current, target);
            return new ConfigDiffDto(
                sd.DeviceId.ToString(),
                deviceNames.GetValueOrDefault(sd.DeviceId) ?? sd.DeviceId.ToString(),
                changes.Select(c => new ConfigChangeDto(c.Path, c.Before, c.After)).ToList(),
                changes.Count == 0);
        }).ToList();
    }

    public async Task<List<ConfigDiffDto>> PreviewPresetAsync(Guid sessionId, Guid presetId, CancellationToken ct)
    {
        var preset = await db.Presets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == presetId, ct)
            ?? throw new KeyNotFoundException($"Preset {presetId} not found");
        var target = ScanConfigParser.Parse(preset.ConfigJson);
        var members = await db.SessionDevices.AsNoTracking().Where(sd => sd.SessionId == sessionId).ToListAsync(ct);
        var deviceNames = await db.Devices.AsNoTracking()
            .Where(d => members.Select(m => m.DeviceId).Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        return members.Select(sd =>
        {
            var current = ScanConfigParser.Parse(sd.ConfigJson);
            var merged = ScanConfigParser.MergeInterfaces(target, current.Interfaces.Select(i => i.Name));
            var changes = ScanConfigDiffer.Diff(current, merged);
            return new ConfigDiffDto(
                sd.DeviceId.ToString(),
                deviceNames.GetValueOrDefault(sd.DeviceId) ?? sd.DeviceId.ToString(),
                changes.Select(c => new ConfigChangeDto(c.Path, c.Before, c.After)).ToList(),
                changes.Count == 0);
        }).ToList();
    }

    public async Task<List<ConfigPushResult>> ApplyPresetAsync(Guid sessionId, Guid presetId, List<Guid>? deviceIds, CancellationToken ct)
    {
        var preset = await db.Presets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == presetId, ct)
            ?? throw new KeyNotFoundException($"Preset {presetId} not found");
        var target = ScanConfigParser.Parse(preset.ConfigJson);
        var members = await db.SessionDevices.AsNoTracking().Where(sd => sd.SessionId == sessionId).ToListAsync(ct);
        var targets = deviceIds is { Count: > 0 }
            ? members.Where(m => deviceIds.Contains(m.DeviceId)).ToList()
            : members;

        var results = new List<ConfigPushResult>(targets.Count);
        foreach (var member in targets)
        {
            var current = ScanConfigParser.Parse(member.ConfigJson);
            var merged = ScanConfigParser.MergeInterfaces(target, current.Interfaces.Select(i => i.Name));
            results.Add(await ApplyToDeviceAsync(sessionId, member.DeviceId, ScanConfigParser.Stringify(merged), presetId, ct));
        }
        return results;
    }
}
