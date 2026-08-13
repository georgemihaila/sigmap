namespace Sigmap.Domain.ScanConfig;

public class InterfaceConfig
{
    public string Name { get; set; } = string.Empty;
    public string Driver { get; set; } = "nl80211";
    public bool MonitorMode { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public List<int> Channels { get; set; } = [];
}

public class ScanConfig
{
    public List<InterfaceConfig> Interfaces { get; set; } = [];
    public int ChannelHopMs { get; set; }
    public bool ScanWifi { get; set; }
    public bool ScanBluetooth { get; set; }
    public bool ScanBtLe { get; set; }
    public bool ScanClientsPromiscuous { get; set; }
    public int BatchIntervalMs { get; set; }
}
