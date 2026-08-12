using Microsoft.Extensions.Logging;
using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent.Capture;

/// <summary>
/// A producer of detections (and optional GPS samples). Real hardware sources
/// (raw AF_PACKET capture over monitor mode, BlueZ) and the synthetic simulator
/// implement the same interface so the pipeline is identical and swappable.
/// </summary>
public interface IScanSource
{
    /// <summary>Short human name for logs, e.g. "wifi" or "simulator".</summary>
    string Name { get; }

    /// <summary>True if this source needs CAP_NET_RAW/CAP_NET_ADMIN or root.</summary>
    bool RequiresPrivileges { get; }

    /// <summary>Starts producing. Runs until the token is cancelled.</summary>
    Task StartAsync(SourceContext context, CancellationToken ct);

    Task StopAsync();
}

/// <summary>Callbacks and live config handed to a running source.</summary>
public sealed class SourceContext
{
    /// <summary>Consume a raw detection as soon as it is observed.</summary>
    public required Action<Detection> OnDetection { get; init; }

    /// <summary>Consume a GPS fix (device has its own receiver).</summary>
    public required Action<GpsSample> OnGpsSample { get; init; }

    /// <summary>Thread-safe snapshot of the current live config.</summary>
    public required Func<ScanConfig> CurrentConfig { get; init; }

    public required ILogger Logger { get; init; }
}
