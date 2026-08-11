using Microsoft.Extensions.Logging;
using Sigmap.Contracts.Proto;
using Tmds.DBus;

namespace Sigmap.Scanner.Agent.Capture.BlueZ;

/// <summary>
/// Bluetooth/BLE source over BlueZ's D-Bus API. Discovers classic BT and BLE
/// devices via the ObjectManager's InterfacesAdded signal and reports RSSI /
/// name / address. No netlink needed — BlueZ's D-Bus API is the supported path.
/// </summary>
public sealed class BlueZSource : IScanSource
{
    public string Name => "bluez";
    public bool RequiresPrivileges => true; // hci access

    private const string BluezService = "org.bluez";
    private const string AdapterPath = "/org/bluez/hci0";

    private readonly ILogger<BlueZSource> _log;

    public BlueZSource(ILogger<BlueZSource> log) => _log = log;

    public async Task StartAsync(SourceContext context, CancellationToken ct)
    {
        var config = context.CurrentConfig();
        var discoverClassic = config.ScanBluetooth;
        var discoverLe = config.ScanBtLe;
        if (!discoverClassic && !discoverLe)
            return;

        var connection = Connection.System;
        await connection.ConnectAsync();

        var adapter = connection.CreateProxy<IAdapter1>(BluezService, new ObjectPath(AdapterPath));
        var objects = connection.CreateProxy<IObjectManager>(BluezService, new ObjectPath("/"));

        await adapter.SetPoweredAsync(true);
        await adapter.StartDiscoveryAsync();
        _log.LogInformation("BlueZ discovery started on {Adapter}", AdapterPath);

        objects.InterfacesAdded += (path, interfaces) =>
        {
            if (!interfaces.TryGetValue("org.bluez.Device1", out var props))
                return;

            try
            {
                var device = context.CurrentConfig().ScanBtLe
                    ? ReadLeDevice(props)
                    : ReadClassicDevice(props);
                if (device is not null)
                    context.OnDetection(device);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Skipping malformed BlueZ device report");
            }
        };

        // Also sweep already-known devices so the first batches are not empty.
        foreach (var (path, interfaces) in await objects.GetManagedObjectsAsync())
        {
            if (interfaces.TryGetValue("org.bluez.Device1", out var props))
            {
                var device = discoverLe ? ReadLeDevice(props) : ReadClassicDevice(props);
                if (device is not null)
                    context.OnDetection(device);
            }
        }

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
        finally
        {
            try
            {
                await adapter.StopDiscoveryAsync();
            }
            catch
            {
                // ignore on shutdown
            }
        }
    }

    private Detection? ReadLeDevice(IDictionary<string, object> props)
    {
        var address = ReadString(props, "Address");
        if (address is null)
            return null;
        return new Detection
        {
            DeviceType = DeviceType.BtLe,
            Mac = address,
            BtUuid = ReadString(props, "UUIDs")?.Split(' ').FirstOrDefault() ?? string.Empty,
            SignalDbm = ReadRssi(props),
            DetectedAtUnixMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }

    private Detection? ReadClassicDevice(IDictionary<string, object> props)
    {
        var address = ReadString(props, "Address");
        if (address is null)
            return null;
        return new Detection
        {
            DeviceType = DeviceType.Bluetooth,
            Mac = address,
            BtName = ReadString(props, "Alias") ?? ReadString(props, "Name") ?? string.Empty,
            SignalDbm = ReadRssi(props),
            DetectedAtUnixMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }

    private static string? ReadString(IDictionary<string, object> props, string key) =>
        props.TryGetValue(key, out var value) && value is string s ? s : null;

    private static int ReadRssi(IDictionary<string, object> props) =>
        props.TryGetValue("RSSI", out var value) && value is short r ? r : 0;

    public Task StopAsync() => Task.CompletedTask;
}

#pragma warning disable CS0067

[DBusInterface("org.bluez.Adapter1")]
internal interface IAdapter1 : IDBusObject
{
    Task SetPoweredAsync(bool powered);
    Task StartDiscoveryAsync();
    Task StopDiscoveryAsync();
}

[DBusInterface("org.freedesktop.DBus.ObjectManager")]
internal interface IObjectManager : IDBusObject
{
    event Action<ObjectPath, IDictionary<string, IDictionary<string, object>>> InterfacesAdded;
    Task<IDictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>>> GetManagedObjectsAsync();
}
