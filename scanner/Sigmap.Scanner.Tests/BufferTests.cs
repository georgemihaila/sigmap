using Microsoft.Extensions.Logging.Abstractions;
using Sigmap.Scanner.Agent.Buffer;
using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Tests;

public class DetectionBufferTests
{
    private static Detection Ap(string mac) => new() { DeviceType = DeviceType.Ap, Mac = mac };

    [Fact]
    public void TakeSnapshot_batches_all_items_and_clears()
    {
        var buffer = new DetectionBuffer();
        buffer.Add(Ap("AA:BB:CC:DD:EE:01"));
        buffer.Add(Ap("AA:BB:CC:DD:EE:02"));
        buffer.AddGps(new GpsSample { Lat = 52.0, Lon = 13.0, AtUnixMs = 1 });

        var batch = buffer.TakeSnapshot("dev-1");

        Assert.NotNull(batch);
        Assert.Equal(2, batch!.Detections.Count);
        Assert.Single(batch.GpsSamples);
        Assert.Equal("dev-1", batch.DeviceId);
        Assert.True(batch.HasGps);
        Assert.False(string.IsNullOrEmpty(batch.BatchId));
        Assert.False(buffer.HasItems);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void TakeSnapshot_returns_null_when_empty()
    {
        var buffer = new DetectionBuffer();
        Assert.Null(buffer.TakeSnapshot("dev-1"));
    }

    [Fact]
    public void Restore_returns_snapshot_contents()
    {
        var buffer = new DetectionBuffer();
        buffer.Add(Ap("AA:BB:CC:DD:EE:03"));
        var snapshot = buffer.TakeSnapshot("dev-1")!;
        Assert.Equal(0, buffer.Count);

        buffer.Restore(snapshot);
        Assert.Equal(1, buffer.Count);
        Assert.True(buffer.HasItems);
    }

    [Fact]
    public void Gps_only_snapshot_is_still_shipped()
    {
        var buffer = new DetectionBuffer();
        buffer.AddGps(new GpsSample { Lat = 52.0, Lon = 13.0, AtUnixMs = 1 });
        var batch = buffer.TakeSnapshot("dev-1");
        Assert.NotNull(batch);
        Assert.Empty(batch!.Detections);
        Assert.True(batch.HasGps);
    }
}

public class SqliteOfflineStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"sigmap-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Persist_peek_delete_round_trip()
    {
        await using var store = new SqliteOfflineStore(_path, NullLogger<SqliteOfflineStore>.Instance);
        var envelope = NewBatchEnvelope("batch-1");
        await store.PersistAsync(envelope, CancellationToken.None);
        await store.PersistAsync(envelope, CancellationToken.None); // idempotent

        Assert.Equal(1, await store.CountAsync(CancellationToken.None));

        var peeked = await store.PeekOldestAsync(10, CancellationToken.None);
        Assert.Single(peeked);
        Assert.Equal("batch-1", peeked[0].DetectionBatch.BatchId);

        await store.DeleteAsync("batch-1", CancellationToken.None);
        Assert.Equal(0, await store.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Peek_returns_oldest_first()
    {
        await using var store = new SqliteOfflineStore(_path, NullLogger<SqliteOfflineStore>.Instance);
        await store.PersistAsync(NewBatchEnvelope("old"), CancellationToken.None);
        await Task.Delay(10);
        await store.PersistAsync(NewBatchEnvelope("new"), CancellationToken.None);

        var peeked = await store.PeekOldestAsync(10, CancellationToken.None);
        Assert.Equal(new[] { "old", "new" }, peeked.Select(e => e.DetectionBatch.BatchId));
    }

    private static Envelope NewBatchEnvelope(string batchId)
    {
        var batch = new DetectionBatch { DeviceId = "dev", BatchId = batchId };
        batch.Detections.Add(new Detection { DeviceType = DeviceType.Ap, Mac = "AA:BB:CC:DD:EE:0F" });
        return Sigmap.Contracts.Messages.EnvelopeFactory.For(batchId, batch);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
