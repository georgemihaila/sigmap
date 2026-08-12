using System.Net.Http;
using System.Text.Json;
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
        var log = logFactory.CreateLogger<ScannerRunner>();
        var offline = new SqliteOfflineStore(options.OfflineDbPath, logFactory.CreateLogger<SqliteOfflineStore>());
        var initial = await LoadInitialConfigAsync(options, offline, log);
        var live = new LiveScanConfig(initial);
        var buffer = new DetectionBuffer();
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

    /// <summary>Startup config priority: backend pull (authoritative) &gt;
    /// last applied config persisted locally &gt; default. Pulling from the
    /// backend lets a fresh or restarted agent resume scanning without a manual
    /// push, and the pulled value is persisted for offline restarts.</summary>
    private static async Task<ScanConfig> LoadInitialConfigAsync(
        ScannerOptions options, SqliteOfflineStore offline, ILogger<ScannerRunner> log)
    {
        var fallback = DefaultConfig(options);

        var remote = await FetchRemoteConfigAsync(options, log, CancellationToken.None);
        if (remote is not null)
        {
            try
            {
                await offline.SaveConfigAsync(remote.ToString(), CancellationToken.None);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to persist pulled config; continuing");
            }

            log.LogInformation("Using config pulled from backend");
            return remote;
        }

        try
        {
            var json = await offline.LoadConfigAsync(CancellationToken.None);
            if (json is null)
            {
                log.LogInformation("Using default config: all scan types off (nothing pulled, nothing persisted)");
                return fallback;
            }
            var config = ScanConfig.Parser.ParseJson(json);
            log.LogInformation(
                "Resumed last applied config: wifi={W} bt={B} ble={L} clients={C} hop={H}ms",
                config.ScanWifi, config.ScanBluetooth, config.ScanBtLe, config.ScanClientsPromiscuous, config.ChannelHopMs);
            return config;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Failed to restore persisted config; using default");
            return fallback;
        }
    }

    /// <summary>Pulls the device's current config from the core API.</summary>
    private static async Task<ScanConfig?> FetchRemoteConfigAsync(
        ScannerOptions options, ILogger<ScannerRunner> log, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.BackendUrl))
        {
            log.LogWarning("Config pull skipped: SCANNER__BACKEND_URL not set");
            return null;
        }

        var url = $"{options.BackendUrl.TrimEnd('/')}/api/v1/devices/{options.DeviceId}/config";
        log.LogInformation("Pulling config from backend: {Url}", url);
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                log.LogInformation("Config pull returned {Status}; using local/default", (int)response.StatusCode);
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var configJson = doc.RootElement.GetProperty("configJson").GetString();
            if (configJson is null)
            {
                log.LogWarning("Config pull returned an empty config; using local/default");
                return null;
            }

            var config = ScanConfig.Parser.ParseJson(configJson);
            log.LogInformation(
                "Pulled config from backend: wifi={W} bt={B} ble={L} clients={C} hop={H}ms",
                config.ScanWifi, config.ScanBluetooth, config.ScanBtLe, config.ScanClientsPromiscuous, config.ChannelHopMs);
            return config;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Config pull from backend failed; using local/default");
            return null;
        }
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

    public async Task ApplyConfig(ScanConfig config, CancellationToken ct)
    {
        _log.LogInformation("Applying config: wifi={W} bt={B} ble={L} clients={C} hop={H}ms",
            config.ScanWifi, config.ScanBluetooth, config.ScanBtLe, config.ScanClientsPromiscuous, config.ChannelHopMs);
        try
        {
            await _offline.SaveConfigAsync(config.ToString(), ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to persist applied config; continuing");
        }

        _live.Set(config);
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token, ct);
        var token = linked.Token;

        var tasks = new[]
        {
            ObserveAsync("config consumer", _configConsumer.RunAsync(token)),
            ObserveAsync("flush coordinator", _flush.RunAsync(token)),
            ObserveAsync("heartbeat sender", _heartbeat.RunAsync(token)),
            ObserveAsync("scan sources", SourcesLoopAsync(token)),
        };

        // Run until cancellation. A single background task completing or failing
        // must never exit the whole agent; failures are logged and we keep going.
        await Task.WhenAll(tasks);
    }

    /// <summary>Runs a background task, logging any fault instead of letting it
    /// kill the process or go unobserved.</summary>
    private async Task ObserveAsync(string name, Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Background task {Name} failed; keeping agent alive", name);
        }
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

            var sources = BuildSources(config);
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

            var sourceTasks = sources.Select(s => RunSourceAsync(s, ctx, iteration.Token)).ToArray();
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

    private async Task RunSourceAsync(IScanSource source, SourceContext ctx, CancellationToken ct)
    {
        try
        {
            await source.StartAsync(ctx, ct);
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Source {Name} failed", source.Name);
        }
    }

    private List<IScanSource> BuildSources(ScanConfig config)
    {
        var sources = new List<IScanSource>();
        if (_options.SourceType.Equals("wifi", StringComparison.OrdinalIgnoreCase))
        {
            var wireless = new IwLinuxWireless(_logFactory.CreateLogger<IwLinuxWireless>());
            sources.Add(new WifiSource(wireless, _logFactory.CreateLogger<WifiSource>()));
            if (config.ScanBluetooth || config.ScanBtLe)
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
