using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Application.Configuration;

/// <summary>Canonical ScanConfig factories shared by seeding and pairing.</summary>
public static class ScanConfigFactory
{
    /// <summary>
    /// The built-in "Off" default: no interfaces, every scan type disabled.
    /// Newly-paired devices start here so nothing scans until a user
    /// deliberately applies a preset.
    /// </summary>
    public static ScanConfig Off() => new()
    {
        ChannelHopMs = 500,
        ScanWifi = false,
        ScanBluetooth = false,
        ScanBtLe = false,
        ScanClientsPromiscuous = false,
        BatchIntervalMs = 1500,
    };

    public static string ToJson(ScanConfig config) =>
        Google.Protobuf.JsonFormatter.Default.Format(config);

    public static ScanConfig FromJson(string json) =>
        Google.Protobuf.JsonParser.Default.Parse<ScanConfig>(json);
}
