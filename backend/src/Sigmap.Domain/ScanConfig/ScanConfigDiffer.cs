namespace Sigmap.Domain.ScanConfig;

/// <summary>One discrete change between two scan configs (for the diff/preview UI).</summary>
public class ConfigChange
{
    public string Path { get; set; } = string.Empty;
    public string Before { get; set; } = string.Empty;
    public string After { get; set; } = string.Empty;
}

/// <summary>Per-device diff used by the preview endpoints.</summary>
public class ConfigDiff
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public List<ConfigChange> Changes { get; set; } = [];
    public bool Equal => Changes.Count == 0;
}

/// <summary>Port of frontend `diffScanConfigs` from `src/lib/scanConfig.ts`.</summary>
public static class ScanConfigDiffer
{
    public static List<ConfigChange> Diff(ScanConfig before, ScanConfig after)
    {
        var changes = new List<ConfigChange>();

        var beforeIfaces = before.Interfaces.ToDictionary(i => i.Name);
        var afterIfaces = after.Interfaces.ToDictionary(i => i.Name);

        foreach (var name in beforeIfaces.Keys)
        {
            if (!afterIfaces.ContainsKey(name))
                changes.Add(new ConfigChange { Path = $"interfaces[{name}]", Before = "present", After = "removed" });
        }
        foreach (var (name, a) in afterIfaces)
        {
            if (!beforeIfaces.TryGetValue(name, out var b))
            {
                changes.Add(new ConfigChange { Path = $"interfaces[{name}]", Before = "absent", After = "added" });
                continue;
            }
            if (a.Enabled != b.Enabled)
                changes.Add(new ConfigChange { Path = $"interfaces[{name}].enabled", Before = Fmt(b.Enabled), After = Fmt(a.Enabled) });
            if (a.MonitorMode != b.MonitorMode)
                changes.Add(new ConfigChange { Path = $"interfaces[{name}].monitorMode", Before = Fmt(b.MonitorMode), After = Fmt(a.MonitorMode) });
            if (a.Driver != b.Driver)
                changes.Add(new ConfigChange { Path = $"interfaces[{name}].driver", Before = b.Driver, After = a.Driver });
            if (string.Join(',', a.Channels) != string.Join(',', b.Channels))
                changes.Add(new ConfigChange
                {
                    Path = $"interfaces[{name}].channels",
                    Before = b.Channels.Count > 0 ? string.Join(',', b.Channels) : "all",
                    After = a.Channels.Count > 0 ? string.Join(',', a.Channels) : "all",
                });
        }

        var scalarInts = new (Func<ScanConfig, int> Get, string Label)[]
        {
            (c => c.ChannelHopMs, "channelHopMs"),
            (c => c.BatchIntervalMs, "batchIntervalMs"),
        };
        foreach (var (get, label) in scalarInts)
        {
            if (get(before) != get(after))
                changes.Add(new ConfigChange { Path = label, Before = Fmt(get(before)), After = Fmt(get(after)) });
        }

        var scalarBools = new (Func<ScanConfig, bool> Get, string Label)[]
        {
            (c => c.ScanWifi, "scanWifi"),
            (c => c.ScanBluetooth, "scanBluetooth"),
            (c => c.ScanBtLe, "scanBtLe"),
            (c => c.ScanClientsPromiscuous, "scanClientsPromiscuous"),
        };
        foreach (var (get, label) in scalarBools)
        {
            if (get(before) != get(after))
                changes.Add(new ConfigChange { Path = label, Before = Fmt(get(before)), After = Fmt(get(after)) });
        }

        return changes;
    }

    private static string Fmt(object? v) => v switch
    {
        null => "null",
        bool b => b ? "true" : "false",
        string s => s,
        _ => System.Text.Json.JsonSerializer.Serialize(v),
    };
}
