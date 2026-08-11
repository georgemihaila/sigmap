using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Sigmap.Backend.Application.Ingestion;
using Sigmap.Backend.Application.Lookup;
using Sigmap.Backend.Application.Messaging;
using Sigmap.Backend.Domain;
using Sigmap.Backend.Domain.Entities;
using Sigmap.Backend.Infrastructure.Persistence;
using Sigmap.Contracts.Geo;
using Sigmap.Contracts.Proto;
using Sigmap.Contracts.Proto.Live;
using Point = NetTopologySuite.Geometries.Point;
using ProtoDetectionBatch = Sigmap.Contracts.Proto.DetectionBatch;
using ProtoDetection = Sigmap.Contracts.Proto.Detection;

namespace Sigmap.Backend.Infrastructure.Ingestion;

/// <summary>
/// Ingests a detection batch: resolves locations against swarm GPS context,
/// writes Postgres in one transaction gated by the (device_id, batch_id)
/// unique constraint, then publishes the located batch for live streaming.
/// Callers ack the broker message only after this returns.
/// </summary>
public sealed class DetectionBatchProcessor : IDetectionBatchProcessor
{
    private static readonly TimeSpan SwarmGpsWindow = TimeSpan.FromMinutes(5);

    private readonly SigmapDbContext _db;
    private readonly ILocationResolver _resolver;
    private readonly IEventPublisher _events;
    private readonly OuiLookup _oui;
    private readonly ILogger<DetectionBatchProcessor> _log;

    public DetectionBatchProcessor(
        SigmapDbContext db,
        ILocationResolver resolver,
        IEventPublisher events,
        OuiLookup oui,
        ILogger<DetectionBatchProcessor> log)
    {
        _db = db;
        _resolver = resolver;
        _events = events;
        _oui = oui;
        _log = log;
    }

    public async Task<IngestResult> ProcessAsync(ProtoDetectionBatch batch, CancellationToken ct)
    {
        if (!Guid.TryParse(batch.DeviceId, out var deviceId))
        {
            _log.LogWarning("Batch from device with unparseable id '{DeviceId}'", batch.DeviceId);
            return new IngestResult(IngestStatus.Rejected, 0);
        }

        var membership = await _db.SessionDevices
            .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId, ct);
        if (membership is null)
        {
            // Device not (yet) assigned to a session — nothing to write.
            _log.LogInformation("Ignoring batch from unassigned device {DeviceId}", batch.DeviceId);
            return new IngestResult(IngestStatus.Handled, 0);
        }

        // Idempotency gate: a redelivered batch is a no-op.
        if (await _db.DetectionBatches.AnyAsync(
                b => b.DeviceId == batch.DeviceId && b.BatchId == batch.BatchId, ct))
            return new IngestResult(IngestStatus.Duplicate, 0);

        var sessionId = membership.SessionId;
        var swarmId = membership.SwarmId;
        var now = DateTimeOffset.UtcNow;

        var tracks = await BuildSwarmTracksAsync(batch, deviceId, sessionId, swarmId, ct);
        var located = ResolveLocations(batch, tracks);

        var batchEntity = new Sigmap.Backend.Domain.Entities.DetectionBatch
        {
            DeviceId = batch.DeviceId,
            BatchId = batch.BatchId,
            SessionId = sessionId,
            ReceivedAt = now,
        };

        var detections = BuildDetectionEntities(batch, batchEntity, deviceId, sessionId, swarmId, located);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        _db.DetectionBatches.Add(batchEntity);
        _db.Detections.AddRange(detections);

        if (batch.HasGps && batch.GpsSamples.Count > 0)
        {
            _db.SessionGpsSamples.AddRange(batch.GpsSamples.Select(s => new SessionGpsSample
            {
                SessionId = sessionId,
                DeviceId = deviceId,
                Geom = new Point(s.Lon, s.Lat) { SRID = 4326 },
                At = DateTimeOffset.FromUnixTimeMilliseconds((long)s.AtUnixMs),
            }));
        }

        await UpsertDetectedDevicesAsync(detections, ct);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _log.LogInformation("Ingested batch {BatchId} from {DeviceId}: {Count} detections ({Unlocated} unlocated)",
            batch.BatchId, batch.DeviceId, detections.Count,
            detections.Count(d => d.LocationFlag == Sigmap.Backend.Domain.LocationFlag.Unlocated));

        await _events.PublishLocatedBatchAsync(BuildLocatedBatch(batch, sessionId, located), ct);
        return new IngestResult(IngestStatus.Handled, detections.Count);
    }

    private async Task<List<DeviceGpsTrack>> BuildSwarmTracksAsync(
        ProtoDetectionBatch batch, Guid deviceId, Guid sessionId, Guid? swarmId, CancellationToken ct)
    {
        var windowStart = nowWindowStart(batch);

        var tracks = new List<DeviceGpsTrack>();

        // Other swarm members' stored GPS tracks (only relevant when grouped).
        if (swarmId is not null)
        {
            var swarmMemberIds = await _db.SessionDevices
                .Where(sd => sd.SessionId == sessionId && sd.SwarmId == swarmId)
                .Select(sd => sd.DeviceId)
                .ToListAsync(ct);

            if (swarmMemberIds.Count > 0)
            {
                var samples = await _db.SessionGpsSamples
                    .Where(s => s.SessionId == sessionId
                                && swarmMemberIds.Contains(s.DeviceId)
                                && s.At >= windowStart)
                    .ToListAsync(ct);

                foreach (var group in samples.GroupBy(s => s.DeviceId))
                {
                    tracks.Add(new DeviceGpsTrack(
                        group.Key.ToString(),
                        true,
                        group.Select(s => new GpsFix(s.Geom.Y, s.Geom.X, 0, s.At.ToUnixTimeMilliseconds()))
                            .OrderBy(f => f.AtUnixMs)
                            .ToList()));
                }
            }
        }

        // Merge this batch's own samples (and stored history) into the source track.
        var ownFixes = batch.GpsSamples
            .Select(s => new GpsFix(s.Lat, s.Lon, s.AccuracyM, (long)s.AtUnixMs))
            .ToList();
        var storedOwn = tracks.FirstOrDefault(t => t.DeviceId == batch.DeviceId);
        if (storedOwn is not null)
            ownFixes = storedOwn.Fixes.Concat(ownFixes).ToList();
        tracks.RemoveAll(t => t.DeviceId == batch.DeviceId);
        if (batch.HasGps || ownFixes.Count > 0)
        {
            tracks.Add(new DeviceGpsTrack(
                batch.DeviceId,
                batch.HasGps,
                ownFixes.OrderBy(f => f.AtUnixMs).ToList()));
        }

        return tracks;
    }

    private static DateTimeOffset nowWindowStart(ProtoDetectionBatch batch)
    {
        var earliest = batch.Detections.Count > 0
            ? batch.Detections.Min(d => (long)d.DetectedAtUnixMs)
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return DateTimeOffset.FromUnixTimeMilliseconds(earliest - (long)SwarmGpsWindow.TotalMilliseconds);
    }

    private List<(ProtoDetection Det, LocationResult Loc)> ResolveLocations(
        ProtoDetectionBatch batch, List<DeviceGpsTrack> tracks)
    {
        var result = new List<(ProtoDetection, LocationResult)>(batch.Detections.Count);
        foreach (var det in batch.Detections)
        {
            var loc = _resolver.Resolve(
                (long)det.DetectedAtUnixMs,
                batch.DeviceId,
                tracks,
                SwarmGpsWindow);
            result.Add((det, loc));
        }

        return result;
    }

    private static List<Domain.Entities.Detection> BuildDetectionEntities(
        ProtoDetectionBatch batch,
        Sigmap.Backend.Domain.Entities.DetectionBatch batchEntity,
        Guid deviceId,
        Guid sessionId,
        Guid? swarmId,
        List<(ProtoDetection Det, LocationResult Loc)> located)
    {
        var entities = new List<Domain.Entities.Detection>(located.Count);
        foreach (var (det, loc) in located)
        {
            entities.Add(new Domain.Entities.Detection
            {
                Batch = batchEntity,
                SessionId = sessionId,
                DeviceId = deviceId,
                SwarmId = swarmId,
                DeviceType = det.DeviceType,
                Mac = det.Mac,
                Ssid = string.IsNullOrEmpty(det.Ssid) ? null : det.Ssid,
                BtName = string.IsNullOrEmpty(det.BtName) ? null : det.BtName,
                BtUuid = string.IsNullOrEmpty(det.BtUuid) ? null : det.BtUuid,
                SignalDbm = det.SignalDbm,
                Channel = (int)det.Channel,
                Encryption = det.Encryption,
                DetectedAt = DateTimeOffset.FromUnixTimeMilliseconds((long)det.DetectedAtUnixMs),
                LocationFlag = ToLocationFlag(loc.Status),
                // PostGIS geometry(Point,4326) stores (longitude, latitude).
                Geom = loc.HasPosition ? new Point(loc.Lon!.Value, loc.Lat!.Value) { SRID = 4326 } : null,
                Source = DetectionSource.Scanner,
            });
        }

        return entities;
    }

    private static Sigmap.Backend.Domain.LocationFlag ToLocationFlag(LocationStatus status) => status switch
    {
        LocationStatus.Direct => Sigmap.Backend.Domain.LocationFlag.Gps,
        LocationStatus.Interpolated => Sigmap.Backend.Domain.LocationFlag.Inferred,
        _ => Sigmap.Backend.Domain.LocationFlag.Unlocated,
    };

    private async Task UpsertDetectedDevicesAsync(IReadOnlyCollection<Domain.Entities.Detection> detections, CancellationToken ct)
    {
        if (detections.Count == 0)
            return;

        var normalForms = detections.Select(d => OuiLookup.NormalizeMac(d.Mac)).ToHashSet();
        var existing = await _db.DetectedDevices
            .Where(dd => normalForms.Contains(dd.MacNormalized))
            .ToDictionaryAsync(dd => dd.MacNormalized, ct);

        foreach (var group in detections.GroupBy(d => OuiLookup.NormalizeMac(d.Mac)))
        {
            var norm = group.Key;
            var first = group.OrderBy(g => g.DetectedAt).First();
            var last = group.OrderByDescending(g => g.DetectedAt).First();

            if (existing.TryGetValue(norm, out var dd))
            {
                if (first.DetectedAt < dd.FirstSeenAt)
                    dd.FirstSeenAt = first.DetectedAt;
                if (last.DetectedAt > dd.LastSeenAt)
                {
                    dd.LastSeenAt = last.DetectedAt;
                    dd.Geom = last.Geom ?? dd.Geom;
                }

                if (last.Ssid is not null)
                    dd.SsidLatest = last.Ssid;
                if (dd.VendorName is null && _oui.TryLookup(first.Mac, out var vendor))
                {
                    dd.VendorOui = vendor.Oui;
                    dd.VendorName = vendor.Name;
                }
            }
            else
            {
                _oui.TryLookup(first.Mac, out var vendor);
                _db.DetectedDevices.Add(new DetectedDevice
                {
                    Mac = group.First().Mac,
                    MacNormalized = norm,
                    VendorOui = vendor.Oui,
                    VendorName = vendor.Name == "Unknown" ? null : vendor.Name,
                    DeviceType = last.DeviceType,
                    SsidLatest = last.Ssid,
                    FirstSeenAt = first.DetectedAt,
                    LastSeenAt = last.DetectedAt,
                    Geom = last.Geom,
                });
            }
        }
    }

    private static LocatedBatch BuildLocatedBatch(
        ProtoDetectionBatch batch, Guid sessionId, List<(ProtoDetection Det, LocationResult Loc)> located)
    {
        var result = new LocatedBatch
        {
            BatchId = batch.BatchId,
            DeviceId = batch.DeviceId,
            SessionId = sessionId.ToString(),
        };
        foreach (var (det, loc) in located)
        {
            result.Detections.Add(new LocatedDetection
            {
                Detection = det,
                Lat = loc.Lat ?? 0,
                Lon = loc.Lon ?? 0,
                LocationFlag = ToProtoFlag(loc.Status),
            });
        }

        return result;
    }

    private static Sigmap.Contracts.Proto.LocationFlag ToProtoFlag(LocationStatus status) => status switch
    {
        LocationStatus.Direct => Sigmap.Contracts.Proto.LocationFlag.Gps,
        LocationStatus.Interpolated => Sigmap.Contracts.Proto.LocationFlag.Inferred,
        _ => Sigmap.Contracts.Proto.LocationFlag.Unlocated,
    };
}
