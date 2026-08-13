using Microsoft.EntityFrameworkCore;
using Sigmap.Application.Dtos;
using Sigmap.Application.Json;
using Sigmap.Application.Services;
using Sigmap.Domain.Entities;
using Sigmap.Domain.Enums;
using Sigmap.Infrastructure.Persistence;

namespace Sigmap.Infrastructure.Services;

public sealed class SessionService(SigmapDbContext db) : ISessionService
{
    public async Task<List<SessionDto>> ListAsync(CancellationToken ct) =>
        await db.Sessions.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => Mappers.ToDto(s))
            .ToListAsync(ct);

    public Task<SessionDto?> GetAsync(Guid id, CancellationToken ct) =>
        db.Sessions.AsNoTracking().Where(s => s.Id == id).Select(s => Mappers.ToDto(s)).FirstOrDefaultAsync(ct);

    public async Task<SessionDto> CreateAsync(SessionInputDto input, CancellationToken ct)
    {
        var session = new Session
        {
            Name = string.IsNullOrWhiteSpace(input.Name) ? "Untitled session" : input.Name,
            Description = input.Description,
            StartsAt = input.StartsAt,
            EndsAt = input.EndsAt,
            Status = SessionStatus.Planned,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);
        return Mappers.ToDto(session);
    }

    public async Task<SessionDto?> UpdateAsync(Guid id, SessionInputDto input, CancellationToken ct)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null) return null;
        if (input.Name is not null) session.Name = input.Name;
        if (input.Description is not null) session.Description = input.Description;
        if (input.StartsAt is not null) session.StartsAt = input.StartsAt;
        if (input.EndsAt is not null) session.EndsAt = input.EndsAt;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Mappers.ToDto(session);
    }

    public async Task<SessionDto?> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null) return null;
        session.Status = SessionStatus.Archived;
        session.EndsAt = DateTimeOffset.UtcNow;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Mappers.ToDto(session);
    }

    public async Task<SessionStatsDto> GetStatsAsync(Guid sessionId, CancellationToken ct)
    {
        var total = await db.Detections.CountAsync(d => d.SessionId == sessionId, ct);
        var located = await db.Detections.CountAsync(d => d.SessionId == sessionId && d.LocationFlag != LocationFlag.Unlocated, ct);

        var encryption = await db.Detections
            .Where(d => d.SessionId == sessionId && d.Encryption != Encryption.Unspecified)
            .GroupBy(d => d.Encryption)
            .Select(g => new { K = g.Key, C = g.Count() })
            .ToDictionaryAsync(g => EnumStrings.ToString(g.K), g => g.C, ct);

        var types = await db.Detections
            .Where(d => d.SessionId == sessionId)
            .GroupBy(d => d.DeviceType)
            .Select(g => new { K = g.Key, C = g.Count() })
            .ToDictionaryAsync(g => EnumStrings.ToString(g.K), g => g.C, ct);

        var vendor = await db.Detections
            .Where(d => d.SessionId == sessionId)
            .Join(db.DetectedDevices,
                d => d.MacNormalized,
                dd => dd.MacNormalized,
                (d, dd) => dd.VendorName)
            .Where(v => v != null)
            .GroupBy(v => v!)
            .Select(g => new { K = g.Key, C = g.Count() })
            .OrderByDescending(g => g.C)
            .Take(12)
            .ToDictionaryAsync(g => g.K, g => g.C, ct);

        var memberDeviceIds = db.SessionDevices.Where(sd => sd.SessionId == sessionId).Select(sd => sd.DeviceId);
        var onlineDevices = await db.Devices.CountAsync(d => memberDeviceIds.Contains(d.Id) && d.Status == DeviceStatus.Online, ct);
        var deviceCount = await memberDeviceIds.CountAsync(ct);

        return new SessionStatsDto(
            total,
            located,
            total - located,
            deviceCount,
            onlineDevices,
            encryption,
            vendor,
            types);
    }

    public async Task<List<SwarmDto>> ListSwarmsAsync(Guid sessionId, CancellationToken ct)
    {
        var swarms = await db.Swarms.AsNoTracking().Where(w => w.SessionId == sessionId).ToListAsync(ct);
        var counts = await db.SessionDevices
            .Where(sd => sd.SessionId == sessionId && sd.SwarmId != null)
            .GroupBy(sd => sd.SwarmId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return swarms.Select(w => new SwarmDto(w.Id, w.SessionId, w.Name, counts.GetValueOrDefault(w.Id))).ToList();
    }

    public async Task<CoverageDto> GetCoverageAsync(Guid sessionId, int limit, CancellationToken ct)
    {
        var points = await db.Detections.AsNoTracking()
            .Where(d => d.SessionId == sessionId && d.Lat != null && d.Lon != null)
            .OrderByDescending(d => d.Id)
            .Take(limit)
            .Select(d => new CoveragePointDto(d.Lat!.Value, d.Lon!.Value))
            .ToListAsync(ct);
        return new CoverageDto(sessionId, points, points.Count);
    }

    public async Task<LiveSessionStateDto> GetLiveSnapshotAsync(Guid sessionId, CancellationToken ct)
    {
        var located = await db.Detections.AsNoTracking()
            .Where(d => d.SessionId == sessionId && d.Lat != null && d.Lon != null)
            .OrderByDescending(d => d.Id)
            .Take(800)
            .ToListAsync(ct);

        var points = new Dictionary<string, LiveMapPointDto>();
        foreach (var d in located)
        {
            points[d.Mac] = new LiveMapPointDto(
                d.Mac,
                d.Lat!.Value,
                d.Lon!.Value,
                d.DeviceType,
                d.Ssid,
                d.SignalDbm,
                d.DetectedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"));
        }

        var memberDeviceIds = db.SessionDevices.Where(sd => sd.SessionId == sessionId).Select(sd => sd.DeviceId);
        var devices = await db.Devices.AsNoTracking().Where(d => memberDeviceIds.Contains(d.Id)).ToListAsync(ct);
        var heartbeats = new Dictionary<string, LiveHeartbeatDto>();
        foreach (var device in devices)
        {
            var caps = Mappers.Capabilities(device.CapabilitiesJson);
            heartbeats[device.Id.ToString()] = new LiveHeartbeatDto(
                device.Id.ToString(),
                (device.LastHeartbeatAt ?? DateTimeOffset.UtcNow).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                device.Status switch { DeviceStatus.Error => "error", _ => "online" },
                device.Status == DeviceStatus.Online ? 6 : null,
                caps.HasBattery ? 78 : null,
                caps.HasGps,
                0,
                0);
        }

        return new LiveSessionStateDto(
            sessionId,
            points,
            [],
            0,
            null,
            heartbeats);
    }
}
