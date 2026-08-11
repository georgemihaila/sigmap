using Microsoft.Extensions.Logging;
using Sigmap.Scanner.Agent.Broker;
using Sigmap.Scanner.Agent.Buffer;
using Sigmap.Scanner.Agent.Capture;
using Sigmap.Scanner.Agent.Capture.BlueZ;
using Sigmap.Scanner.Agent.Capture.Simulator;
using Sigmap.Scanner.Agent.Capture.Wifi;
using Sigmap.Scanner.Agent.Linux;
using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent;

/// <summary>Wires sources → buffer → flush coordinator → broker, plus heartbeat,
/// config consumption and offline-first persistence.</summary>
public sealed class ScannerRunner : IAsyncDisposable
{
    private readonly ScannerOptions _options;
    private readonly LiveScanConfig _live;
    private readonly DetectionBuffer _buffer;
    private readonly SqliteOfflineStore _offline;
    private readonly RabbitMqPublisher _publisher;
    private readonly FlushCoordinator _flush;
    private readonly HeartbeatSender _heartbeat;
    private readonly ConfigConsumer _configConsumer;
    private readonly ILogger<ScannerRunner> _log;
    private readonly ILoggerFactory _logFactory;
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<ScanConfig> _currentConfig;

    private ScannerRunner(ScannerOptions options, LiveScanConfig live, DetectionBuffer buffer,
        SqliteOfflineStore offline, RabbitMqPublisher publisher, FlushCoordinator flush,
        HeartbeatSender heartbeat, ILoggerFactory logFactory)
    {
        _options = options;
        _live = live;
        _buffer = buffer;
        _offline = offline;
        _publisher = publisher;
        _flush = flush;
        _heartbeat = heartbeat;
        _log = logFactory.CreateLogger<ScannerRunner>();
        _logFactory = logFactory;
        _currentConfig = () => _live.Value;

        _configConsumer = new ConfigConsumer(
            options.DeviceId,
            ApplyConfig,
            publisher,
            logFactory.CreateLogger<ConfigConsumer>());
    }

    public static async Task<ScannerRunner> CreateAsync(ScannerOptions options, ILoggerFactory logFactory)
    {
        var live = new LiveScanConfig(DefaultConfig(options));
        var buffer = new DetectionBuffer();
        var offline = new SqliteOfflineStore(options.OfflineDbPath, logFactory.CreateLogger<SqliteOfflineStore>());
        var publisher = new RabbitMqPublisher(options.RabbitMqUri, logFactory.CreateLogger<RabbitMqPublisher>());
        var flushInterval = TimeSpan.FromMilliseconds(live.Value.BatchIntervalMs > 0 ? live.Value.BatchIntervalMs : 1500);
        var flush = new FlushCoordinator(buffer, publisher, offline, options.DeviceId, flushInterval, logFactory.CreateLogger<FlushCoordinator>());
        var heartbeat = new HeartbeatSender(
            publisher, options.DeviceId,
            TimeSpan.FromMilliseconds(options.HeartbeatIntervalMs),
            buffer, () => flush.TotalSent,
            logFactory.CreateLogger<HeartbeatSender>());
        return new ScannerRunner(options, live, buffer, offline, publisher, flush, heartbeat, logFactory);
    }

    private static ScanConfig DefaultConfig(ScannerOptions options) => options.DefaultConfig ?? new ScanConfig
    {
        ChannelHopMs = 500,
        ScanWifi = false,
        ScanBluetooth = false,
        ScanBtLe = false,
        ScanClientsPromiscuous = false,
        BatchIntervalMs = 1500,
    };

    public void ApplyConfig(ScanConfig config)
    {
        _log.LogInformation("Applying config: wifi={W} bt={B} ble={L} clients={C} hop={H}ms",
            config.ScanWifi, config.ScanBluetooth, config.ScanBtLe, config.ScanClientsPromiscuous, config.ChannelHopMs);
        _live.Set(config);
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token, ct);
        var token = linked.Token;

        var configTask = _configConsumer.RunAsync(token);
        var flushTask = _flush.RunAsync(token);
        var heartbeatTask = _heartbeat.RunAsync(token);
        var sourcesTask = SourcesLoopAsync(token);

        await Task.WhenAny(configTask, flushTask, heartbeatTask, sourcesTask);
        token.ThrowIfCancellationRequested();
    }

    private async Task SourcesLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var config = _live.Value;
            if (!ShouldScan(config))
            {
                _log.LogInformation("No scan types enabled; idle until a config push arrives");
                try
                {
                    await Task.Delay(1000, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                continue;
            }

            var sources = BuildSources();
            if (sources.Count == 0)
            {
                try
                {
                    await Task.Delay(1000, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                continue;
            }

            using var iteration = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var ctx = new SourceContext
            {
                OnDetection = _buffer.Add,
                OnGpsSample = _buffer.AddGps,
                CurrentConfig = _currentConfig,
                Logger = _log,
            };

            _log.LogInformation("Starting {Count} source(s): {Names}",
                sources.Count, string.Join(", ", sources.Select(s => s.Name)));

            var sourceTasks = sources.Select(s => s.StartAsync(ctx, iteration.Token)).ToArray();
            try
            {
                await _live.ChangedAsync(iteration.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                iteration.Cancel();
                try
                {
                    await Task.WhenAll(sourceTasks);
                }
                catch
                {
                    // sources stop on cancel; ignore shutdown noise
                }
            }
        }
    }

    private bool ShouldScan(ScanConfig config) =>
        config.ScanWifi || config.ScanBluetooth || config.ScanBtLe || config.ScanClientsPromiscuous;

    private List<IScanSource> BuildSources()
    {
        var sources = new List<IScanSource>();
        if (_options.SourceType.Equals("wifi", StringComparison.OrdinalIgnoreCase))
        {
            var wireless = new IwLinuxWireless(_logFactory.CreateLogger<IwLinuxWireless>());
            sources.Add(new SharpPcapWifiSource(wireless, _logFactory.CreateLogger<SharpPcapWifiSource>()));
            sources.Add(new BlueZSource(_logFactory.CreateLogger<BlueZSource>()));
        }
        else
        {
            sources.Add(new SimulatorSource());
        }

        return sources;
    }

    public void Stop()
    {
        _stop.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        _stop.Dispose();
        await _offline.DisposeAsync();
        await _publisher.DisposeAsync();
    }
}
