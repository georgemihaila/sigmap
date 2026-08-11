using Microsoft.Extensions.Logging;
using Sigmap.Scanner.Agent;
using Sigmap.Contracts.Proto;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    using var logFactory = LoggerFactory.Create(b =>
    {
        b.AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss ");
        if (Environment.GetEnvironmentVariable("SCANNER__DEBUG") == "1")
            b.SetMinimumLevel(LogLevel.Debug);
    });
    var log = logFactory.CreateLogger("Sigmap.Scanner");

    var options = LoadOptions();
    if (string.IsNullOrWhiteSpace(options.DeviceId))
        options.DeviceId = LoadOrCreateDeviceId(options.DeviceIdPath) ?? Guid.NewGuid().ToString("N");

    await using var runner = await ScannerRunner.CreateAsync(options, logFactory);

    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        log.LogInformation("Shutdown requested");
        runner.Stop();
    };

    log.LogInformation(
        "Sigmap scanner agent starting: device={DeviceId} source={Source} rabbitmq={Uri}",
        options.DeviceId, options.SourceType, options.RabbitMqUri);

    try
    {
        await runner.RunAsync();
    }
    catch (OperationCanceledException)
    {
        // graceful shutdown
    }

    log.LogInformation("Scanner agent stopped");
    return 0;
}

static ScannerOptions LoadOptions()
{
    var options = new ScannerOptions();
    options.DeviceId = Env("SCANNER__DEVICE_ID", string.Empty);
    options.RabbitMqUri = Env("SCANNER__RABBITMQ_URI", options.RabbitMqUri);
    options.SourceType = Env("SCANNER__SOURCE_TYPE", options.SourceType);
    options.OfflineDbPath = Env("SCANNER__OFFLINE_DB", options.OfflineDbPath);
    options.DeviceIdPath = Env("SCANNER__DEVICE_ID_FILE", options.DeviceIdPath);
    options.SimulateGps = !string.Equals(Env("SCANNER__SIMULATE_GPS", "true"), "false", StringComparison.OrdinalIgnoreCase);
    if (int.TryParse(Env("SCANNER__HEARTBEAT_MS", string.Empty), out var hb))
        options.HeartbeatIntervalMs = hb;
    options.DefaultConfig = new ScanConfig
    {
        ChannelHopMs = 500,
        ScanWifi = false,
        ScanBluetooth = false,
        ScanBtLe = false,
        ScanClientsPromiscuous = false,
        BatchIntervalMs = 1500,
    };
    return options;
}

static string? LoadOrCreateDeviceId(string path)
{
    try
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path).Trim();
            if (existing.Length > 0)
                return existing;
        }

        var id = Guid.NewGuid().ToString("N");
        File.WriteAllText(path, id);
        return id;
    }
    catch
    {
        return null;
    }
}

static string Env(string name, string fallback) =>
    Environment.GetEnvironmentVariable(name) ?? fallback;
