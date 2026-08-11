using Microsoft.EntityFrameworkCore;
using Sigmap.Contracts.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using Sigmap.Backend.Application.Ingestion;
using Sigmap.Backend.Domain.Entities;
using Sigmap.Backend.Infrastructure.Ingestion;
using Sigmap.Backend.Infrastructure.Messaging;
using Sigmap.Backend.Infrastructure.Persistence;
using Sigmap.Contracts.Geo;
using Sigmap.Contracts.Proto;
using Sigmap.Contracts.Proto.Live;
using DomainLocationFlag = Sigmap.Backend.Domain.LocationFlag;

namespace Sigmap.Backend.IntegrationTests;

[Collection("integration")]
public class IngestTests
{
    private readonly IntegrationFixture _fixture;

    public IngestTests(IntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Gps_device_batch_is_persisted_with_direct_locations_and_event_published()
    {
        await using var db = TestDb.CreateDb(_fixture.PostgresConnectionString);
        await db.Database.MigrateAsync();
        var ctx = await SeedContextAsync(db, "gps");

        var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var batch = NewBatch(ctx.DeviceAId, "b1", hasGps: true, atMs: t);
        batch.GpsSamples.Add(new GpsSample { Lat = 52.52, Lon = 13.405, AccuracyM = 3, AtUnixMs = (ulong)t });
        batch.Detections.Add(NewApDetection("AA:BB:CC:DD:EE:01", atMs: t));

        var processor = CreateProcessor(db);
        var result = await processor.ProcessAsync(batch, CancellationToken.None);

        Assert.Equal(IngestStatus.Handled, result.Status);
        Assert.Equal(1, result.DetectionCount);

        var saved = await db.Detections.SingleAsync(d => d.Mac == "AA:BB:CC:DD:EE:01");
        Assert.Equal(DomainLocationFlag.Gps, saved.LocationFlag);
        Assert.NotNull(saved.Geom);
        Assert.Equal(13.405, saved.Geom!.X, 3);
        Assert.Equal(52.52, saved.Geom.Y, 3);

        var detected = await db.DetectedDevices.SingleAsync(d => d.MacNormalized == "aabbccddee01");
        Assert.Equal("AA:BB:CC:DD:EE:01", detected.Mac);
        Assert.NotNull(detected.Geom);

        // The located batch must have reached the events exchange (BFF queue).
        var evt = await ConsumeEventAsync(_fixture.RabbitMqConnectionString,
            e => e.Detections is not null && e.Detections.BatchId == "b1");
        Assert.NotNull(evt);
        Assert.NotNull(evt!.Detections);
        Assert.Single(evt.Detections.Detections);
        Assert.Equal(Sigmap.Contracts.Proto.LocationFlag.Gps, evt.Detections.Detections[0].LocationFlag);
        Assert.Equal(ctx.SessionId.ToString(), evt.Detections.SessionId);
    }

    [Fact]
    public async Task Redelivered_batch_is_deduplicated()
    {
        await using var db = TestDb.CreateDb(_fixture.PostgresConnectionString);
        await db.Database.MigrateAsync();
        var ctx = await SeedContextAsync(db, "dedupe");

        var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var batch = NewBatch(ctx.DeviceAId, "same-batch", hasGps: true, atMs: t);
        batch.GpsSamples.Add(new GpsSample { Lat = 52.0, Lon = 13.0, AccuracyM = 3, AtUnixMs = (ulong)t });
        batch.Detections.Add(NewApDetection("AA:BB:CC:DD:EE:02", atMs: t));

        var processor = CreateProcessor(db);
        var first = await processor.ProcessAsync(batch, CancellationToken.None);
        var second = await processor.ProcessAsync(batch, CancellationToken.None);

        Assert.Equal(IngestStatus.Handled, first.Status);
        Assert.Equal(IngestStatus.Duplicate, second.Status);

        var count = await db.Detections.CountAsync(d => d.Mac == "AA:BB:CC:DD:EE:02");
        Assert.Equal(1, count);
        Assert.Equal(1, await db.DetectionBatches.CountAsync(b => b.BatchId == "same-batch"));
    }

    [Fact]
    public async Task No_gps_device_in_swarm_with_gps_member_is_interpolated()
    {
        await using var db = TestDb.CreateDb(_fixture.PostgresConnectionString);
        await db.Database.MigrateAsync();
        var ctx = await SeedContextAsync(db, "swarm");

        // GPS member sends samples first so they land in session_gps_samples.
        var t0 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var gpsBatch = NewBatch(ctx.DeviceAId, "gps-1", hasGps: true, atMs: t0);
        gpsBatch.GpsSamples.Add(new GpsSample { Lat = 52.0, Lon = 13.0, AccuracyM = 3, AtUnixMs = (ulong)(t0 - 2000) });
        gpsBatch.GpsSamples.Add(new GpsSample { Lat = 52.02, Lon = 13.02, AccuracyM = 3, AtUnixMs = (ulong)(t0 + 2000) });
        gpsBatch.Detections.Add(NewApDetection("AA:BB:CC:DD:EE:03", atMs: t0));
        await CreateProcessor(db).ProcessAsync(gpsBatch, CancellationToken.None);

        // Blind device's detection lands between the two fixes.
        var blindBatch = NewBatch(ctx.DeviceBId, "blind-1", hasGps: false, atMs: t0);
        blindBatch.Detections.Add(NewApDetection("AA:BB:CC:DD:EE:04", atMs: t0));

        var result = await CreateProcessor(db).ProcessAsync(blindBatch, CancellationToken.None);

        var saved = await db.Detections.SingleAsync(d => d.Mac == "AA:BB:CC:DD:EE:04");
        Assert.Equal(DomainLocationFlag.Inferred, saved.LocationFlag);
        Assert.NotNull(saved.Geom);
        // Interpolated between (52.0,13.0) and (52.02,13.02) at the midpoint.
        Assert.Equal(13.01, saved.Geom!.X, 2);
        Assert.Equal(52.01, saved.Geom.Y, 2);
    }

    [Fact]
    public async Task No_gps_evidence_flags_unlocated_and_never_fabricates()
    {
        await using var db = TestDb.CreateDb(_fixture.PostgresConnectionString);
        await db.Database.MigrateAsync();
        var ctx = await SeedContextAsync(db, "noloc", assignSwarm: false);

        var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var batch = NewBatch(ctx.DeviceAId, "nl-1", hasGps: false, atMs: t);
        batch.Detections.Add(NewApDetection("AA:BB:CC:DD:EE:05", atMs: t));

        var result = await CreateProcessor(db).ProcessAsync(batch, CancellationToken.None);

        var saved = await db.Detections.SingleAsync(d => d.Mac == "AA:BB:CC:DD:EE:05");
        Assert.Equal(DomainLocationFlag.Unlocated, saved.LocationFlag);
        Assert.Null(saved.Geom);

        var detected = await db.DetectedDevices.SingleAsync(d => d.MacNormalized == "aabbccddee05");
        Assert.Null(detected.Geom);
    }

    [Fact]
    public async Task Gps_device_without_swarm_still_gets_direct_locations()
    {
        await using var db = TestDb.CreateDb(_fixture.PostgresConnectionString);
        await db.Database.MigrateAsync();
        var ctx = await SeedContextAsync(db, "noswarmgps", assignSwarm: false);

        var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var batch = NewBatch(ctx.DeviceAId, "nsg-1", hasGps: true, atMs: t);
        batch.GpsSamples.Add(new GpsSample { Lat = 52.5, Lon = 13.5, AccuracyM = 3, AtUnixMs = (ulong)t });
        batch.Detections.Add(NewApDetection("AA:BB:CC:DD:EE:06", atMs: t));

        var result = await CreateProcessor(db).ProcessAsync(batch, CancellationToken.None);
        Assert.Equal(IngestStatus.Handled, result.Status);

        var saved = await db.Detections.SingleAsync(d => d.Mac == "AA:BB:CC:DD:EE:06");
        Assert.Equal(DomainLocationFlag.Gps, saved.LocationFlag);
        Assert.NotNull(saved.Geom);
    }

    private static Sigmap.Contracts.Proto.DetectionBatch NewBatch(Guid deviceId, string batchId, bool hasGps, long atMs)
    {
        var batch = new Sigmap.Contracts.Proto.DetectionBatch
        {
            DeviceId = deviceId.ToString(),
            BatchId = batchId,
            HasGps = hasGps,
        };
        return batch;
    }

    private static Sigmap.Contracts.Proto.Detection NewApDetection(string mac, long atMs) => new()
    {
        DeviceType = DeviceType.Ap,
        Mac = mac,
        Ssid = "TestNet",
        SignalDbm = -55,
        Channel = 6,
        Encryption = Encryption.Wpa2,
        DetectedAtUnixMs = (ulong)atMs,
    };

    private static async Task<SeedContext> SeedContextAsync(
        SigmapDbContext db, string prefix, bool assignSwarm = true)
    {
        var session = new Session { Id = Guid.NewGuid(), Name = $"t-{prefix}" };
        db.Sessions.Add(session);
        var swarm = new Swarm { Id = Guid.NewGuid(), SessionId = session.Id, Name = "swarm" };
        db.Swarms.Add(swarm);
        var devA = new Device { Id = Guid.NewGuid(), Name = $"{prefix}-A" };
        var devB = new Device { Id = Guid.NewGuid(), Name = $"{prefix}-B" };
        db.Devices.AddRange(devA, devB);
        db.SessionDevices.Add(new SessionDevice
        {
            SessionId = session.Id,
            DeviceId = devA.Id,
            SwarmId = assignSwarm ? swarm.Id : null,
        });
        db.SessionDevices.Add(new SessionDevice
        {
            SessionId = session.Id,
            DeviceId = devB.Id,
            SwarmId = assignSwarm ? swarm.Id : null,
        });
        await db.SaveChangesAsync();
        return new SeedContext(session.Id, devA.Id, devB.Id, swarm.Id);
    }

    private sealed record SeedContext(Guid SessionId, Guid DeviceAId, Guid DeviceBId, Guid SwarmId);

    private DetectionBatchProcessor CreateProcessor(SigmapDbContext db)
    {
        var provider = new RabbitMqConnectionProvider(_fixture.RabbitMqConnectionString, NullLogger<RabbitMqConnectionProvider>.Instance);
        var events = new RabbitMqEventPublisher(provider, NullLogger<RabbitMqEventPublisher>.Instance);
        return new DetectionBatchProcessor(
            db,
            new TimeInterpolationLocationResolver(),
            events,
            new Application.Lookup.OuiLookup(),
            NullLogger<DetectionBatchProcessor>.Instance);
    }

    private async Task<LiveEvent?> ConsumeEventAsync(string connectionString, Func<LiveEvent, bool> match)
    {
        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        await using var conn = await factory.CreateConnectionAsync();
        await using var channel = await conn.CreateChannelAsync(null);
        await RabbitMqTopology.DeclareAsync(channel, CancellationToken.None);

        for (var i = 0; i < 100; i++)
        {
            var result = await channel.BasicGetAsync(RabbitMqTopology.BffQueue, true);
            if (result is null)
                break;
            var envelope = Envelope.Parser.ParseFrom(result.Body.ToArray());
            if (envelope.LiveEvent is not null && match(envelope.LiveEvent))
                return envelope.LiveEvent;
        }

        return null;
    }
}
