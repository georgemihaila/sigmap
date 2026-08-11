using Microsoft.Extensions.Logging;
using SharpPcap;
using SharpPcap.LibPcap;
using Sigmap.Scanner.Agent.ChannelHop;
using Sigmap.Scanner.Agent.Linux;
using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent.Capture.Wifi;

/// <summary>
/// Real WiFi source: libpcap capture (via SharpPcap) on monitor-mode
/// interfaces, channel hopping through <see cref="ILinuxWireless"/>, radiotap
/// signal extraction and 802.11 management-frame parsing. Requires
/// CAP_NET_RAW + CAP_NET_ADMIN.
/// </summary>
public sealed class SharpPcapWifiSource : IScanSource
{
    public string Name => "wifi";
    public bool RequiresPrivileges => true;

    private readonly ILinuxWireless _wireless;
    private readonly ILogger<SharpPcapWifiSource> _log;
    private readonly List<ILiveDevice> _devices = new();

    public SharpPcapWifiSource(ILinuxWireless wireless, ILogger<SharpPcapWifiSource> log)
    {
        _wireless = wireless;
        _log = log;
    }

    public async Task StartAsync(SourceContext context, CancellationToken ct)
    {
        var config = context.CurrentConfig();
        var enabled = config.Interfaces.Where(i => i.Enabled && i.MonitorMode).ToList();
        if (enabled.Count == 0)
        {
            _log.LogWarning("Wifi source enabled but no monitor interfaces configured");
            return;
        }

        foreach (var iface in enabled)
        {
            ct.ThrowIfCancellationRequested();
            await _wireless.SetMonitorModeAsync(iface.Name, true, ct);
            var device = OpenInterface(iface.Name);
            _devices.Add(device);
            _ = CaptureLoopAsync(device, iface, context, ct);
        }

        // Keep the source alive until cancelled.
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    private ILiveDevice OpenInterface(string name)
    {
        var device = LibPcapLiveDeviceList.Instance.FirstOrDefault(d => d.Name == name)
            ?? throw new InvalidOperationException($"No libpcap device named '{name}'");
        device.Open(new DeviceConfiguration { Mode = DeviceModes.Promiscuous });
        device.Filter = "type mgt subtype beacon or type mgt subtype probe-req or type mgt subtype probe-resp";
        return device;
    }

    private async Task CaptureLoopAsync(ILiveDevice device, InterfaceConfig iface, SourceContext context, CancellationToken ct)
    {
        try
        {
            var hopper = new ChannelHopper(
                iface.Channels.Count > 0 ? iface.Channels.Select(c => (int)c).ToList() : DefaultChannels(),
                TimeSpan.FromMilliseconds(context.CurrentConfig().ChannelHopMs));

            var hopTask = Task.Run(() => hopper.HopLoopAsync(
                async (ch, token) => await _wireless.SetChannelAsync(iface.Name, ch, token), ct), ct);

            while (!ct.IsCancellationRequested)
            {
                var status = device.GetNextPacket(out var capture);
                if (status != GetPacketStatus.PacketRead)
                    continue;

                var data = capture.Data;
                var radiotap = RadiotapParser.Parse(data);
                var frame = Dot11FrameParser.Parse(data[radiotap.HeaderLength..]);
                if (frame.Kind == Dot11FrameParser.FrameKind.None)
                    continue;

                var config = context.CurrentConfig();
                var scanClients = frame.Kind == Dot11FrameParser.FrameKind.ProbeRequest;
                if (frame.Kind == Dot11FrameParser.FrameKind.Beacon && !config.ScanWifi)
                    continue;
                if (scanClients && !config.ScanClientsPromiscuous)
                    continue;

                var detection = new Detection
                {
                    DeviceType = scanClients ? DeviceType.Client : DeviceType.Ap,
                    Mac = frame.Bssid,
                    Ssid = frame.Ssid ?? string.Empty,
                    SignalDbm = radiotap.SignalDbm ?? 0,
                    Channel = (uint)(radiotap.Channel ?? (int)hopper.CurrentChannel),
                    DetectedAtUnixMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    VendorOui = Google.Protobuf.ByteString.CopyFrom(System.Text.Encoding.UTF8.GetBytes(frame.Bssid.Replace(":", string.Empty)[..6])),
                };
                context.OnDetection(detection);
            }

            await hopTask;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            device.Close();
        }
    }

    private static List<int> DefaultChannels() => new() { 1, 6, 11 };

    public async Task StopAsync()
    {
        foreach (var device in _devices)
            device.Close();
        await Task.CompletedTask;
    }
}
