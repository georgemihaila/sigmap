using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent;

/// <summary>Runtime configuration for the scanner agent. Values come from
/// environment / config file; the scanning parameters themselves arrive from
/// the backend as ConfigPush messages and live in <see cref="ScanConfig"/>.</summary>
public sealed class ScannerOptions
{
    public const string SectionName = "Scanner";

    /// <summary>Stable device id; persisted locally on first run so reconnects
    /// are idempotent. Overridable via env SCANNER__DEVICE_ID.</summary>
    public string DeviceId { get; set; } = string.Empty;

    public string RabbitMqUri { get; set; } = "amqp://sigmap:sigmap@localhost:5672";
    public string OfflineDbPath { get; set; } = "scanner-buffer.db";
    public string DeviceIdPath { get; set; } = "device.id";

    /// <summary>Core API base URL the agent pulls its config from on startup
    /// (so a fresh/crashed agent resumes scanning without a manual push).</summary>
    public string BackendUrl { get; set; } = "http://localhost:5080";

    /// <summary>"simulator" (no privileges) | "wifi" (monitor mode + caps).</summary>
    public string SourceType { get; set; } = "simulator";

    public int HeartbeatIntervalMs { get; set; } = 15000;

    /// <summary>Simulator emits synthetic GPS samples along a route.</summary>
    public bool SimulateGps { get; set; } = true;

    public ScanConfig DefaultConfig { get; set; } = null!;
}
