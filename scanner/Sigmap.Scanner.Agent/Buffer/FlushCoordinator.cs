using Microsoft.Extensions.Logging;
using Sigmap.Contracts.Messages;
using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent.Buffer;

/// <summary>
/// Flushes the live buffer to the broker every interval. If the broker is
/// unreachable the snapshot is persisted to the offline store and replayed
/// (oldest first) once the connection recovers. Batch ids are preserved across
/// offline replays so the backend's idempotency gate holds.
/// </summary>
public sealed class FlushCoordinator
{
    private readonly DetectionBuffer _buffer;
    private readonly IDetectionPublisher _publisher;
    private readonly IOfflineStore _offline;
    private readonly string _deviceId;
    private readonly TimeSpan _flushInterval;
    private readonly ILogger<FlushCoordinator> _log;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public FlushCoordinator(
        DetectionBuffer buffer,
        IDetectionPublisher publisher,
        IOfflineStore offline,
        string deviceId,
        TimeSpan flushInterval,
        ILogger<FlushCoordinator> log)
        : this(buffer, publisher, offline, deviceId, flushInterval, log, static (t, ct) => Task.Delay(t, ct))
    {
    }

    internal FlushCoordinator(
        DetectionBuffer buffer,
        IDetectionPublisher publisher,
        IOfflineStore offline,
        string deviceId,
        TimeSpan flushInterval,
        ILogger<FlushCoordinator> log,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _buffer = buffer;
        _publisher = publisher;
        _offline = offline;
        _deviceId = deviceId;
        _flushInterval = flushInterval;
        _log = log;
        _delay = delay;
    }

    public int OfflinePending { get; private set; }

    /// <summary>Total detections confirmed sent since start (live + replayed).</summary>
    public long TotalSent { get; private set; }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // 1) Replay offline batches first (oldest first).
                await DrainOfflineAsync(ct);

                // 2) Flush whatever accumulated live.
                var snapshot = _buffer.TakeSnapshot(_deviceId);
                if (snapshot is not null)
                {
                    _log.LogDebug("Flushing snapshot: {Detections} detections, {Gps} gps samples, {Buffered} buffered",
                        snapshot.Detections.Count, snapshot.GpsSamples.Count, _buffer.Count);
                    await FlushLiveAsync(snapshot, ct);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Flush cycle failed");
            }

            try
            {
                await _delay(_flushInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DrainOfflineAsync(CancellationToken ct)
    {
        var pending = await _offline.CountAsync(ct);
        if (pending == 0)
        {
            OfflinePending = 0;
            return;
        }

        _log.LogInformation("Broker reachable; replaying {Count} offline batches", pending);
        foreach (var envelope in await _offline.PeekOldestAsync(100, ct))
        {
            try
            {
                await _publisher.PublishAsync(envelope, ct);
                await _offline.DeleteAsync(envelope.DetectionBatch.BatchId, ct);
                TotalSent += envelope.DetectionBatch.Detections.Count;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Broker dropped again mid-replay; stop and wait for next cycle.
                _log.LogWarning(ex, "Offline replay interrupted; will retry");
                break;
            }
        }

        OfflinePending = await _offline.CountAsync(ct);
    }

    private async Task FlushLiveAsync(DetectionBatch snapshot, CancellationToken ct)
    {
        var envelope = EnvelopeFactory.For(snapshot.BatchId, snapshot);
        try
        {
            await _publisher.PublishAsync(envelope, ct);
            TotalSent += snapshot.Detections.Count;
        }
        catch (OperationCanceledException)
        {
            _buffer.Restore(snapshot);
            throw;
        }
        catch (Exception ex)
        {
            // Broker unreachable: persist the snapshot so it is not lost.
            _log.LogWarning(ex, "Publish failed; persisting batch {BatchId} offline", snapshot.BatchId);
            try
            {
                await _offline.PersistAsync(envelope, ct);
            }
            catch (Exception persistEx)
            {
                _log.LogError(persistEx, "Offline persist failed; returning batch to memory buffer");
                _buffer.Restore(snapshot);
            }
        }
    }
}
