using System.Text.Json;

namespace Sigmap.Domain.ScanConfig;

/// <summary>
/// Port of frontend `src/lib/scanConfig.ts`: parse/normalize/diff/merge of the
/// JSON scan config the UI edits and pushes to scanners.
/// </summary>
public static class ScanConfigParser
{
    public static ScanConfig Off() => new()
    {
        Interfaces = [],
        ChannelHopMs = 0,
        ScanWifi = false,
        ScanBluetooth = false,
        ScanBtLe = false,
        ScanClientsPromiscuous = false,
        BatchIntervalMs = 1500,
    };

    public static ScanConfig Default() => new()
    {
        Interfaces = [new InterfaceConfig { Name = "wlan0" }],
        ChannelHopMs = 500,
        ScanWifi = true,
        ScanBluetooth = false,
        ScanBtLe = true,
        ScanClientsPromiscuous = false,
        BatchIntervalMs = 1500,
    };

    public static ScanConfig Parse(string? json) => ParseRaw(json);

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static ScanConfig ParseRaw(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Off();
        try
        {
            var raw = JsonSerializer.Deserialize<RawScanConfig>(json, JsonOpts);
            return raw is null ? Off() : Normalize(raw);
        }
        catch (JsonException)
        {
            return Off();
        }
    }

    private sealed class RawInterface
    {
        public string? Name { get; set; }
        public string? Driver { get; set; }
        public bool MonitorMode { get; set; }
        public bool Enabled { get; set; }
        public List<int>? Channels { get; set; }
    }

    private sealed class RawScanConfig
    {
        public List<RawInterface>? Interfaces { get; set; }
        public int? ChannelHopMs { get; set; }
        public bool? ScanWifi { get; set; }
        public bool? ScanBluetooth { get; set; }
        public bool? ScanBtLe { get; set; }
        public bool? ScanClientsPromiscuous { get; set; }
        public int? BatchIntervalMs { get; set; }
    }

    private static ScanConfig Normalize(RawScanConfig raw)
    {
        var baseCfg = Off();
        return new ScanConfig
        {
            Interfaces = raw.Interfaces is not null
                ? raw.Interfaces
                    .Where(i => !string.IsNullOrEmpty(i.Name))
                    .Select(i => new InterfaceConfig
                    {
                        Name = i.Name!,
                        Driver = i.Driver ?? "nl80211",
                        MonitorMode = i.MonitorMode,
                        Enabled = i.Enabled,
                        Channels = i.Channels ?? [],
                    })
                    .ToList()
                : baseCfg.Interfaces,
            ChannelHopMs = raw.ChannelHopMs ?? baseCfg.ChannelHopMs,
            ScanWifi = raw.ScanWifi ?? baseCfg.ScanWifi,
            ScanBluetooth = raw.ScanBluetooth ?? baseCfg.ScanBluetooth,
            ScanBtLe = raw.ScanBtLe ?? baseCfg.ScanBtLe,
            ScanClientsPromiscuous = raw.ScanClientsPromiscuous ?? baseCfg.ScanClientsPromiscuous,
            BatchIntervalMs = raw.BatchIntervalMs ?? baseCfg.BatchIntervalMs,
        };
    }

    public static string Stringify(ScanConfig config) => JsonSerializer.Serialize(config);

    /// <summary>Merge target interfaces with the names a device actually reports.</summary>
    public static ScanConfig MergeInterfaces(ScanConfig target, IEnumerable<string> currentNames)
    {
        var names = currentNames.ToList();
        if (names.Count == 0 || target.Interfaces.Count > 0) return target;
        return new ScanConfig
        {
            Interfaces = names.Select(n => new InterfaceConfig { Name = n }).ToList(),
            ChannelHopMs = target.ChannelHopMs,
            ScanWifi = target.ScanWifi,
            ScanBluetooth = target.ScanBluetooth,
            ScanBtLe = target.ScanBtLe,
            ScanClientsPromiscuous = target.ScanClientsPromiscuous,
            BatchIntervalMs = target.BatchIntervalMs,
        };
    }
}
