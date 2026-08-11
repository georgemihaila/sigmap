using Microsoft.Extensions.Logging.Abstractions;
using Sigmap.Scanner.Agent.Buffer;
using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Tests;

public class FlushCoordinatorTests
{
    private static Detection Ap(string mac) => new() { DeviceType = DeviceType.Ap, Mac = mac };

    private sealed class FakePublisher : IDetectionPublisher
    {
        public bool Fail { get; set; }
        public List<Envelope> Published { get; } = new();

        public Task PublishAsync(Envelope envelope, CancellationToken ct)
        {
            if (Fail)
                throw new IOException("broker unreachable");
            Published.Add(envelope);
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryStore : IOfflineStore
    {
        public List<Envelope> Batches { get; } = new();
        public Task PersistAsync(Envelope e, CancellationToken ct) { Batches.Add(e); return Task.CompletedTask; }
        public Task<IReadOnlyList<Envelope>> PeekOldestAsync(int max, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envelope>>(Batches.Take(max).ToList());
        public Task DeleteAsync(string batchId, CancellationToken ct) { Batches.RemoveAll(b => b.DetectionBatch.BatchId == batchId); return Task.CompletedTask; }
        public Task<int> CountAsync(CancellationToken ct) => Task.FromResult(Batches.Count);
    }

    /// <summary>Delay that lets N cycles run then throws to stop the loop.</summary>
    private sealed class CycleDelay
    {
        public int Remaining { get; set; } = 3;
        public Task Delay(TimeSpan _, CancellationToken ct)
        {
            if (Remaining-- <= 0)
                return Task.FromCanceled(new CancellationToken(true));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Publishes_live_snapshot_and_updates_sent_total()
    {
        var buffer = new DetectionBuffer();
        buffer.Add(Ap("AA:BB:CC:DD:EE:01"));
        var publisher = new FakePublisher();
        var store = new MemoryStore();
        var delay = new CycleDelay { Remaining = 2 };

        var coordinator = new FlushCoordinator(buffer, publisher, store, "dev-1",
            TimeSpan.FromSeconds(1), NullLogger<FlushCoordinator>.Instance, delay.Delay);

        await coordinator.RunAsync(CancellationToken.None);

        Assert.Single(publisher.Published);
        Assert.Single(publisher.Published[0].DetectionBatch.Detections);
        Assert.Equal(1, coordinator.TotalSent);
        Assert.Empty(store.Batches);
    }

    [Fact]
    public async Task Broker_down_persists_batch_offline_then_replays_on_recovery()
    {
        var buffer = new DetectionBuffer();
        buffer.Add(Ap("AA:BB:CC:DD:EE:02"));
        var publisher = new FakePublisher { Fail = true };
        var store = new MemoryStore();
        var delay = new CycleDelay { Remaining = 3 };

        var coordinator = new FlushCoordinator(buffer, publisher, store, "dev-1",
            TimeSpan.FromSeconds(1), NullLogger<FlushCoordinator>.Instance, delay.Delay);

        await coordinator.RunAsync(CancellationToken.None);

        // Batch was persisted, not lost, and not counted as sent.
        Assert.Empty(publisher.Published);
        Assert.Single(store.Batches);
        Assert.Equal(0, coordinator.TotalSent);
        Assert.Equal(0, buffer.Count);

        // Recovery: broker comes back, replay drains offline store.
        publisher.Fail = false;
        delay.Remaining = 3;
        await coordinator.RunAsync(CancellationToken.None);

        Assert.Single(publisher.Published);
        Assert.Empty(store.Batches);
        Assert.Equal(1, coordinator.TotalSent);
    }

    [Fact]
    public async Task Batch_id_survives_offline_replay()
    {
        var buffer = new DetectionBuffer();
        buffer.Add(Ap("AA:BB:CC:DD:EE:03"));
        var publisher = new FakePublisher { Fail = true };
        var store = new MemoryStore();
        var delay = new CycleDelay { Remaining = 2 };

        var coordinator = new FlushCoordinator(buffer, publisher, store, "dev-1",
            TimeSpan.FromSeconds(1), NullLogger<FlushCoordinator>.Instance, delay.Delay);
        await coordinator.RunAsync(CancellationToken.None);

        var persistedBatchId = store.Batches[0].DetectionBatch.BatchId;

        publisher.Fail = false;
        delay.Remaining = 2;
        await coordinator.RunAsync(CancellationToken.None);

        // Same batch id on replay ⇒ backend idempotency holds.
        Assert.Equal(persistedBatchId, publisher.Published[0].DetectionBatch.BatchId);
    }
}
