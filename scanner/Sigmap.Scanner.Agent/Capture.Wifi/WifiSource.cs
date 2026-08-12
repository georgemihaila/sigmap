using Microsoft.Extensions.Logging;
using Sigmap.Scanner.Agent.ChannelHop;
using Sigmap.Scanner.Agent.Linux;
using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent.Capture.Wifi;

/// <summary>
/// Real WiFi source: raw AF_PACKET capture on monitor-mode interfaces (no
/// libpcap/SharpPcap), channel hopping through <see cref="ILinuxWireless"/>,
/// radiotap signal extraction and 802.11 management-frame parsing — all done
/// in-process. Requires CAP_NET_RAW + CAP_NET_ADMIN.
/// </summary>
public sealed class WifiSource : IScanSource
{
    public string Name => "wifi";
    public bool RequiresPrivileges => true;

    private readonly ILinuxWireless _wireless;
    private readonly ILogger<WifiSource> _log;
    private readonly List<RawPacketSocket> _sockets = new();

    public WifiSource(ILinuxWireless wireless, ILogger<WifiSource> log)
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
            var socket = OpenInterface(iface.Name);
            _sockets.Add(socket);
            _ = CaptureLoopAsync(socket, iface, context, ct);
        }

        // Keep the source alive until cancelled.
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    private RawPacketSocket OpenInterface(string name)
    {
        var socket = new RawPacketSocket();
        socket.Open(name);
        return socket;
    }

    private async Task CaptureLoopAsync(RawPacketSocket socket, InterfaceConfig iface, SourceContext context, CancellationToken ct)
    {
        long packetsRead = 0;
        long detectionsProduced = 0;
        long lastPacketAt = 0;
        long attemptStartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var reopenCount = 0;
        try
        {
            _log.LogInformation("wifi capture loop started on {Iface}", iface.Name);

            var hopper = new ChannelHopper(
                iface.Channels.Count > 0 ? iface.Channels.Select(c => (int)c).ToList() : DefaultChannels(),
                TimeSpan.FromMilliseconds(context.CurrentConfig().ChannelHopMs));

            var hopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            // Channel hopping is best-effort: if the driver rejects a hop, log
            // once and keep capturing on the current channel instead of killing
            // the whole source.
            var hopWarned = false;
            async Task Hop(int ch, CancellationToken token)
            {
                _log.LogDebug("hop -> channel {Ch}", ch);
                try
                {
                    await _wireless.SetChannelAsync(iface.Name, ch, token);
                    _log.LogDebug("hop channel {Ch} ok", ch);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _log.LogDebug("hop channel {Ch} FAILED: {Msg}", ch, ex.Message);
                    if (!hopWarned)
                    {
                        hopWarned = true;
                        _log.LogWarning(ex,
                            "Channel hopping on {Iface} failed; staying on the current channel (further failures suppressed)",
                            iface.Name);
                    }
                }
            }

            var hopTask = Task.Run(() => hopper.HopLoopAsync(Hop, hopCts.Token), ct);
            var healthTask = Task.Run(() => HealthLoopAsync(
                () => Volatile.Read(ref packetsRead),
                () => Volatile.Read(ref detectionsProduced),
                () => Volatile.Read(ref lastPacketAt),
                () => socket?.LastError ?? 0,
                hopper, ct), ct);

            void ResetBaseline()
            {
                Interlocked.Exchange(ref lastPacketAt, 0);
                Volatile.Write(ref attemptStartedAt, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }

            // Closes and re-opens the AF_PACKET socket. Some USB drivers (e.g.
            // rtl88x2au/rtw88) tear the netdev down on channel hops, which
            // leaves the socket fd stale: recv() never returns readable and the
            // agent gets zero packets even though a fresh handle (or tcpdump)
            // captures fine.
            async Task ReopenAsync()
            {
                _log.LogWarning("Re-opening capture socket on {Iface} (attempt {N})", iface.Name, ++reopenCount);
                try
                {
                    socket.Close();
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, "Close failed while re-opening {Iface}", iface.Name);
                }

                // The driver reset the netdev on a hop, so re-assert monitor
                // mode and a known channel before opening a fresh socket;
                // otherwise the new socket binds to a down/stale interface and
                // still receives nothing.
                try
                {
                    await _wireless.SetMonitorModeAsync(iface.Name, true, ct);
                    await _wireless.SetChannelAsync(iface.Name, hopper.FirstChannel, ct);
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, "Interface reset while re-opening {Iface} failed; continuing", iface.Name);
                }

                try
                {
                    socket = OpenInterface(iface.Name);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Re-open of {Iface} failed; will retry", iface.Name);
                    socket = null!;
                }

                ResetBaseline();
            }

            while (!ct.IsCancellationRequested)
            {
                if (socket is null)
                {
                    _log.LogWarning("Capture socket unavailable on {Iface}; retrying in 2s", iface.Name);
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    try
                    {
                        socket = OpenInterface(iface.Name);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Re-open of {Iface} failed", iface.Name);
                    }

                    continue;
                }

                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var last = Volatile.Read(ref lastPacketAt);
                var silentFor = last > 0 ? now - last : now - Volatile.Read(ref attemptStartedAt);

                var len = socket.Read();
                if (len < 0)
                {
                    // Hopping produced nothing for a while: the driver is
                    // likely resetting the radio on every hop. Disable hopping,
                    // park on the first channel and re-open the socket.
                    if (!hopCts.IsCancellationRequested && silentFor >= 10_000)
                    {
                        hopCts.Cancel();
                        try
                        {
                            await _wireless.SetChannelAsync(iface.Name, hopper.FirstChannel, ct);
                        }
                        catch
                        {
                            // best effort
                        }

                        _log.LogWarning(
                            "No packets for {Seconds}s while hopping; disabled hopping, staying on channel {Ch} (recv errno {Errno})",
                            silentFor / 1000, hopper.FirstChannel, socket.LastError);
                        await ReopenAsync();
                        continue;
                    }

                    // Even without hopping the socket can be stale; re-open a
                    // few times to self-heal, then keep polling.
                    if (silentFor >= 20_000 && reopenCount < 5)
                    {
                        await ReopenAsync();
                        continue;
                    }

                    continue;
                }

                Interlocked.Increment(ref packetsRead);
                Interlocked.Exchange(ref lastPacketAt, now);

                var data = socket.Received;
                _log.LogDebug("pkt len={Len}", data.Length);
                var radiotap = RadiotapParser.Parse(data);
                var frame = Dot11FrameParser.Parse(data[radiotap.HeaderLength..]);
                if (frame.Kind == Dot11FrameParser.FrameKind.None)
                {
                    _log.LogDebug("pkt dropped: radiotap={Radiotap} len={Len}", radiotap.HeaderLength, data.Length);
                    continue;
                }

                var config = context.CurrentConfig();
                var scanClients = frame.Kind == Dot11FrameParser.FrameKind.ProbeRequest;
                if (frame.Kind == Dot11FrameParser.FrameKind.Beacon && !config.ScanWifi)
                {
                    _log.LogDebug("pkt dropped: beacon but scanWifi off");
                    continue;
                }
                if (scanClients && !config.ScanClientsPromiscuous)
                {
                    _log.LogDebug("pkt dropped: probe-req but client detection off");
                    continue;
                }

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
                Interlocked.Increment(ref detectionsProduced);
                _log.LogDebug("pkt -> {Kind} {Bssid} sig={Signal}dBm ch={Channel}",
                    frame.Kind, frame.Bssid, radiotap.SignalDbm, detection.Channel);
            }

            await hopTask;
            await healthTask;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _log.LogDebug("wifi capture loop exiting");
            socket?.Close();
        }
    }

    private async Task HealthLoopAsync(
        Func<long> packets, Func<long> detections, Func<long> lastPacketAt, Func<int> lastError,
        ChannelHopper hopper, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var last = lastPacketAt();
            var ago = last > 0
                ? $"{Math.Max(0, (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - last)) / 1000}s ago"
                : "never";
            var errno = lastError();
            _log.LogInformation(
                "wifi source health: {Packets} packets, {Detections} detections, on channel {Channel}, last packet {Ago}, recv errno {Errno}",
                packets(), detections(), hopper.CurrentChannel, ago, errno);
        }
    }

    private static List<int> DefaultChannels() => new() { 1, 6, 11 };

    public async Task StopAsync()
    {
        foreach (var socket in _sockets)
            socket.Close();
        await Task.CompletedTask;
    }
}
