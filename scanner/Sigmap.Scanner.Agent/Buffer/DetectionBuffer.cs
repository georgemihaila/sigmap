using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent.Buffer;

/// <summary>
/// Thread-safe accumulation of raw detections and GPS samples until the flush
/// coordinator snapshots them into a batch. A snapshot is removed atomically;
/// if the caller cannot persist it, it must call <see cref="RestoreAsync"/> so
/// nothing is silently lost.
/// </summary>
public sealed class DetectionBuffer
{
    private readonly object _lock = new();
    private readonly List<Detection> _detections = new();
    private readonly List<GpsSample> _gpsSamples = new();
    private readonly int _maxBuffered;

    public DetectionBuffer(int maxBuffered = 100_000)
    {
        _maxBuffered = maxBuffered;
    }

    public void Add(Detection detection)
    {
        lock (_lock)
        {
            if (_detections.Count >= _maxBuffered)
                _detections.RemoveAt(0);
            _detections.Add(detection);
        }
    }

    public void AddGps(GpsSample sample)
    {
        lock (_lock)
        {
            if (_gpsSamples.Count >= _maxBuffered)
                _gpsSamples.RemoveAt(0);
            _gpsSamples.Add(sample);
        }
    }

    public bool HasItems
    {
        get
        {
            lock (_lock)
            {
                return _detections.Count > 0 || _gpsSamples.Count > 0;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _detections.Count + _gpsSamples.Count;
            }
        }
    }

    /// <summary>Atomically removes everything and returns a batch, or null when empty.</summary>
    public DetectionBatch? TakeSnapshot(string deviceId)
    {
        lock (_lock)
        {
            if (_detections.Count == 0 && _gpsSamples.Count == 0)
                return null;

            var batch = new DetectionBatch
            {
                DeviceId = deviceId,
                BatchId = Guid.NewGuid().ToString("N"),
                HasGps = _gpsSamples.Count > 0,
            };
            batch.Detections.AddRange(_detections);
            batch.GpsSamples.AddRange(_gpsSamples);
            _detections.Clear();
            _gpsSamples.Clear();
            return batch;
        }
    }

    /// <summary>Returns a snapshot's contents (used when persistence failed).</summary>
    public void Restore(DetectionBatch batch)
    {
        lock (_lock)
        {
            _detections.AddRange(batch.Detections);
            _gpsSamples.AddRange(batch.GpsSamples);
        }
    }
}
