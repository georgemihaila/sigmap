using Sigmap.Domain.ScanConfig;
using Xunit;

namespace Sigmap.Domain.Tests;

public class ScanConfigDifferTests
{
    [Fact]
    public void NoChangesForIdenticalConfigs()
    {
        var a = ScanConfigParser.Default();
        var b = ScanConfigParser.Parse(ScanConfigParser.Stringify(a));
        Assert.Empty(ScanConfigDiffer.Diff(a, b));
    }

    [Fact]
    public void DetectsToggleChange()
    {
        var a = ScanConfigParser.Default();
        var b = ScanConfigParser.Parse(ScanConfigParser.Stringify(a));
        b.ScanBluetooth = !a.ScanBluetooth;
        var changes = ScanConfigDiffer.Diff(a, b);
        Assert.Single(changes);
        Assert.Equal("scanBluetooth", changes[0].Path);
        Assert.Equal(a.ScanBluetooth.ToString().ToLowerInvariant(), changes[0].Before);
        Assert.Equal(b.ScanBluetooth.ToString().ToLowerInvariant(), changes[0].After);
    }

    [Fact]
    public void DetectsInterfaceAddAndRemove()
    {
        var a = ScanConfigParser.Default();
        var b = ScanConfigParser.Parse(ScanConfigParser.Stringify(a));
        b.Interfaces.Add(new InterfaceConfig { Name = "wlan1", Driver = "nl80211", MonitorMode = true, Enabled = true });

        var add = ScanConfigDiffer.Diff(a, b);
        Assert.Contains(add, c => c.Path == "interfaces[wlan1]" && c.Before == "absent" && c.After == "added");

        var remove = ScanConfigDiffer.Diff(b, a);
        Assert.Contains(remove, c => c.Path == "interfaces[wlan1]" && c.Before == "present" && c.After == "removed");
    }

    [Fact]
    public void DetectsInterfacePropertyAndChannelChanges()
    {
        var a = ScanConfigParser.Default();
        var b = ScanConfigParser.Parse(ScanConfigParser.Stringify(a));
        b.Interfaces[0].MonitorMode = false;
        b.Interfaces[0].Channels = [1, 6, 11];
        var changes = ScanConfigDiffer.Diff(a, b);
        Assert.Contains(changes, c => c.Path == "interfaces[wlan0].monitorMode" && c.Before == "true" && c.After == "false");
        Assert.Contains(changes, c => c.Path == "interfaces[wlan0].channels" && c.Before == "all" && c.After == "1,6,11");
    }

    [Fact]
    public void DetectsScalarChanges()
    {
        var a = ScanConfigParser.Default();
        var b = ScanConfigParser.Parse(ScanConfigParser.Stringify(a));
        b.ChannelHopMs = 900;
        b.BatchIntervalMs = 2500;
        var changes = ScanConfigDiffer.Diff(a, b);
        Assert.Contains(changes, c => c.Path == "channelHopMs" && c.Before == "500" && c.After == "900");
        Assert.Contains(changes, c => c.Path == "batchIntervalMs" && c.Before == "1500" && c.After == "2500");
    }
}

public class ScanConfigParserTests
{
    [Fact]
    public void FallsBackToOffConfigOnInvalidInput()
    {
        var off = ScanConfigParser.Off();
        Assert.Equal(off.ChannelHopMs, ScanConfigParser.Parse("not json").ChannelHopMs);
        Assert.Equal(off.ChannelHopMs, ScanConfigParser.Parse(null).ChannelHopMs);
        Assert.False(ScanConfigParser.Parse("not json").ScanWifi);
    }

    [Fact]
    public void NormalizesPartialConfig()
    {
        var cfg = ScanConfigParser.Parse("{\"channelHopMs\":700}");
        Assert.Equal(700, cfg.ChannelHopMs);
        Assert.False(cfg.ScanWifi);
    }
}

public class ScanConfigMergeTests
{
    [Fact]
    public void KeepsExistingInterfacesIntact()
    {
        var target = ScanConfigParser.Default();
        var merged = ScanConfigParser.MergeInterfaces(target, []);
        Assert.Same(target, merged);
    }

    [Fact]
    public void PopulatesInterfacesFromDeviceWhenTargetHasNone()
    {
        var target = ScanConfigParser.Off();
        var merged = ScanConfigParser.MergeInterfaces(target, ["wlan0", "wlan1"]);
        Assert.Equal(["wlan0", "wlan1"], merged.Interfaces.Select(i => i.Name));
        Assert.True(merged.Interfaces[0].Enabled);
        Assert.True(merged.Interfaces[0].MonitorMode);
    }
}
