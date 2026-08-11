using System.Globalization;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent.Capture.Simulator;

/// <summary>
/// Synthetic scan source: emits AP/BT detections and (optionally) GPS fixes so
/// the whole pipeline runs without wireless hardware or elevated privileges.
/// Emits a burst every second; the flush coordinator batches them.
/// </summary>
public sealed class SimulatorSource : IScanSource
{
    public string Name => "simulator";
    public bool RequiresPrivileges => false;

    private readonly Random _rng;
    private readonly GpsSimulator _gps;
    private readonly int _detectionsPerBurst;

    public SimulatorSource(int seed = 42, int detectionsPerBurst = 6)
    {
        _rng = new Random(seed);
        _gps = new GpsSimulator(52.5200, 13.4050, seed);
        _detectionsPerBurst = detectionsPerBurst;
    }

    public Task StartAsync(SourceContext context, CancellationToken ct)
    {
        return Task.Run(async () =>
        {
            context.Logger.LogInformation("Simulator source started");
            while (!ct.IsCancellationRequested)
            {
                var config = context.CurrentConfig();
                if (config.ScanWifi || config.ScanBluetooth || config.ScanBtLe)
                    EmitBurst(context, config);
                else
                    context.Logger.LogDebug("Simulator burst skipped: all scan types off (wifi={W} bt={B} ble={L})",
                        config.ScanWifi, config.ScanBluetooth, config.ScanBtLe);

                context.OnGpsSample(_gps.NextFix(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, ct);
    }

    private void EmitBurst(SourceContext context, ScanConfig config)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        try
        {
            for (var i = 0; i < _detectionsPerBurst; i++)
            {
                if (config.ScanWifi)
                    context.OnDetection(RandomAp(nowMs));
                if (config.ScanBluetooth || config.ScanBtLe)
                    context.OnDetection(RandomBt(nowMs, config.ScanBtLe));
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Simulator burst failed");
            throw;
        }
    }

    private Detection RandomAp(long nowMs)
    {
        var mac = RandomMac();
        return new Detection
        {
            DeviceType = DeviceType.Ap,
            Mac = mac,
            Ssid = $"SimNet-{mac[^2..]}",
            SignalDbm = -90 + _rng.Next(40),
            Channel = (uint)(1 + _rng.Next(13)),
            Encryption = (Encryption)(1 + _rng.Next(5)),
            DetectedAtUnixMs = (ulong)nowMs,
            VendorOui = ByteString.CopyFrom(System.Text.Encoding.UTF8.GetBytes(mac.Substring(0, 8))),
        };
    }

    private Detection RandomBt(long nowMs, bool le)
    {
        return new Detection
        {
            DeviceType = le ? DeviceType.BtLe : DeviceType.Bluetooth,
            Mac = RandomMac(),
            // protobuf strings are never null; empty means "not present".
            BtName = le ? string.Empty : $"SimBT-{_rng.Next(100, 999)}",
            SignalDbm = -100 + _rng.Next(35),
            DetectedAtUnixMs = (ulong)nowMs,
        };
    }

    private string RandomMac()
    {
        // Locally administered unicast MACs.
        var b = new byte[6];
        _rng.NextBytes(b);
        b[0] = (byte)((b[0] & 0xFE) | 0x02);
        return string.Join(":", b.Select(x => x.ToString("X2", CultureInfo.InvariantCulture)));
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
