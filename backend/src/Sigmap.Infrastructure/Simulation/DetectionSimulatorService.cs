using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sigmap.Application.Abstractions;
using Sigmap.Domain;
using Sigmap.Domain.Entities;
using Sigmap.Domain.Enums;
using Sigmap.Infrastructure.Persistence;
using Sigmap.Infrastructure.Services;

namespace Sigmap.Infrastructure.Simulation;

/// <summary>
/// Generates synthetic detection batches for active sessions so the live map
/// works without any wireless hardware — the same role the MSW mock played.
/// </summary>
public sealed class DetectionSimulatorService(
    ILiveEventBus liveBus,
    IServiceScopeFactory scopeFactory,
    ILogger<DetectionSimulatorService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(1200);
    private readonly SimRng _rng = new(20260812);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Simulator tick failed");
            }
            await Task.Delay(TickInterval, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SigmapDbContext>();

        var activeSessionIds = await db.Sessions.AsNoTracking()
            .Where(s => s.Status == SessionStatus.Active)
            .Select(s => s.Id)
            .ToListAsync(ct);
        if (activeSessionIds.Count == 0) return;

        var memberships = await db.SessionDevices.AsNoTracking()
            .Where(sd => activeSessionIds.Contains(sd.SessionId))
            .ToListAsync(ct);
        var deviceIds = memberships.Select(m => m.DeviceId).Distinct().ToList();
        var devices = await db.Devices.AsNoTracking()
            .Where(d => deviceIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);
        var sessions = await db.Sessions.AsNoTracking()
            .Where(s => activeSessionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var onlineBySession = memberships
            .Where(m => devices.TryGetValue(m.DeviceId, out var d) && d.Status == DeviceStatus.Online)
            .GroupBy(m => m.SessionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (sessionId, members) in onlineBySession)
        {
            if (members.Count == 0) continue;
            var member = _rng.Pick(members);
            var device = devices[member.DeviceId];
            var session = sessions[sessionId];
            var count = _rng.Int(1, 6);

            var events = await AppendBatchAsync(db, session, member, device, count, ct);
            foreach (var e in events) liveBus.Publish(e);

            if (_rng.Next() < 0.25)
            {
                liveBus.Publish(new LiveEvent.Device
                {
                    SessionId = sessionId,
                    Heartbeat = HeartbeatFor(device),
                });
            }
        }
    }

    private async Task<List<LiveEvent>> AppendBatchAsync(
        SigmapDbContext db,
        Domain.Entities.Session session,
        SessionDevice member,
        Device device,
        int count,
        CancellationToken ct)
    {
        var sessionId = session.Id;
        var batchId = Guid.NewGuid().ToString("N");
        var detectedAt = DateTimeOffset.UtcNow;
        var area = (session.LatMin, session.LonMin, session.LatMax, session.LonMax);
        var hasArea = area.LatMin is not null && area.LonMin is not null && area.LatMax is not null && area.LonMax is not null;

        var located = new List<LocatedDetection>(count);
        var detections = new List<Detection>(count);

        foreach (var _ in Enumerable.Range(0, count))
        {
            var (oui, vendor) = DataGenerators.OuiFor(_rng);
            var mac = DataGenerators.RandomMac(_rng, oui);
            var macNormalized = MacNormalizer.Normalize(mac);
            var type = DataGenerators.RandomDeviceType(_rng);
            var roll = _rng.Next();
            var locationFlag = roll < 0.55 ? LocationFlag.Gps : roll < 0.8 ? LocationFlag.Inferred : LocationFlag.Unlocated;
            (double Lat, double Lon)? point = locationFlag == LocationFlag.Unlocated || !hasArea
                ? null
                : DataGenerators.PointInArea(_rng, area.LatMin!.Value, area.LonMin!.Value, area.LatMax!.Value, area.LonMax!.Value);
            var ssid = type is DeviceType.Ap ? DataGenerators.RandomSsid(_rng) : type is DeviceType.Client ? DataGenerators.RandomClientSsid(_rng) : null;
            var btName = type is DeviceType.Ap or DeviceType.Client ? null : DataGenerators.RandomBtName(_rng);
            var encryption = type == DeviceType.Ap ? DataGenerators.RandomEncryption(_rng) : Encryption.Unspecified;
            var channel = DataGenerators.ChannelFor(type, _rng);
            var signal = DataGenerators.SignalFor(type, _rng);

            detections.Add(new Detection
            {
                BatchId = batchId,
                SessionId = sessionId,
                DeviceId = device.Id,
                SwarmId = member.SwarmId,
                DeviceType = type,
                Mac = mac,
                MacNormalized = macNormalized,
                Ssid = ssid,
                BtName = btName,
                Channel = channel,
                SignalDbm = signal,
                Encryption = encryption,
                DetectedAt = detectedAt,
                LocationFlag = locationFlag,
                Lat = point?.Lat,
                Lon = point?.Lon,
                Source = DetectionSource.Scanner,
            });

            located.Add(new LocatedDetection
            {
                Mac = mac,
                DeviceType = type,
                Ssid = ssid,
                BtName = btName,
                SignalDbm = signal,
                Channel = channel,
                Encryption = encryption,
                DetectedAt = detectedAt,
                LocationFlag = locationFlag,
                Lat = point?.Lat,
                Lon = point?.Lon,
                VendorName = vendor,
            });
        }

        db.Detections.AddRange(detections);

        // Upsert detected-devices aggregate heads for this batch.
        var normalizedMacs = detections.Select(d => d.MacNormalized).ToList();
        var heads = await db.DetectedDevices.Where(h => normalizedMacs.Contains(h.MacNormalized)).ToDictionaryAsync(h => h.MacNormalized, ct);
        foreach (var d in detections)
        {
            if (heads.TryGetValue(d.MacNormalized, out var head))
            {
                head.LastSeenAt = detectedAt;
                head.DetectionCount += 1;
                head.ChannelLatest = d.Channel;
                if (d.Ssid is not null) head.SsidLatest = d.Ssid;
                if (d.BtName is not null) head.BtNameLatest = d.BtName;
                if (d.Lat is not null) { head.Latitude = d.Lat; head.Longitude = d.Lon; }
            }
            else
            {
                var vendor = DataGenerators.OuiTable.FirstOrDefault(o => d.Mac.StartsWith(o.Oui, StringComparison.OrdinalIgnoreCase)).Vendor ?? null;
                var newHead = new DetectedDevice
                {
                    Mac = d.Mac,
                    MacNormalized = d.MacNormalized,
                    VendorOui = d.Mac[..8],
                    VendorName = vendor,
                    DeviceType = d.DeviceType,
                    SsidLatest = d.Ssid,
                    BtNameLatest = d.BtName,
                    ChannelLatest = d.Channel,
                    FirstSeenAt = detectedAt,
                    LastSeenAt = detectedAt,
                    Latitude = d.Lat,
                    Longitude = d.Lon,
                    DetectionCount = 1,
                };
                heads[d.MacNormalized] = newHead;
                db.DetectedDevices.Add(newHead);
            }
        }

        await db.SaveChangesAsync(ct);

        return
        [
            new LiveEvent.Detections
            {
                SessionId = sessionId,
                BatchId = batchId,
                DeviceId = device.Id,
                Items = located,
            },
        ];
    }

    private Heartbeat HeartbeatFor(Domain.Entities.Device device)
    {
        var caps = Mappers.Capabilities(device.CapabilitiesJson);
        return new Heartbeat
        {
            DeviceId = device.Id,
            At = DateTimeOffset.UtcNow,
            Status = device.Status == DeviceStatus.Error ? "error" : "online",
            CurrentChannel = device.Status == DeviceStatus.Online ? _rng.Int(1, 11) : null,
            BatteryPct = caps.HasBattery ? _rng.Int(40, 100) : null,
            GpsFix = caps.HasGps,
            DetectionsBuffered = _rng.Int(0, 200),
            DetectionsSentTotal = _rng.Int(500, 50000),
        };
    }
}
