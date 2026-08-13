using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sigmap.Domain;
using Sigmap.Domain.Entities;
using Sigmap.Domain.Enums;
using Sigmap.Domain.ScanConfig;
using Sigmap.Infrastructure.Persistence;
using Sigmap.Infrastructure.Services;
using Sigmap.Infrastructure.Simulation;

namespace Sigmap.Infrastructure.Seeding;

/// <summary>
/// Idempotent seed of the same deterministic dataset the MSW mock generated
/// (users, presets, fleet, sessions, detections, exports, pairings).
/// </summary>
public sealed class DbSeeder(SigmapDbContext db, ILogger<DbSeeder> logger) : IDbSeeder
{
    private readonly SimRng _rng = new(20260812);
    private readonly PasswordHasher<User> _hasher = new();

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var operatorId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = operatorId, Username = "operator", Role = UserRole.Operator, PasswordHash = Hash("sigmap-dev") },
            new User { Id = viewerId, Username = "viewer", Role = UserRole.Viewer, PasswordHash = Hash("viewer") });

        SeedPresets(operatorId);
        SeedFleet();
        var sessions = SeedSessions();
        var activeOrArchived = sessions.Where(s => s.Status != SessionStatus.Planned).ToList();
        SeedMemberships(activeOrArchived, operatorId);
        SeedDetections(activeOrArchived);
        SeedExports(activeOrArchived, operatorId);
        SeedPairings();
        db.AppSettings.Add(new AppSetting { Id = 1, WigleApiName = "wigle.net" });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded Sigmap database");
    }

    private string Hash(string password) => _hasher.HashPassword(new User(), password);

    private void SeedPresets(Guid operatorId)
    {
        var off = ScanConfigParser.Off();
        var urban = new ScanConfig
        {
            ChannelHopMs = 250,
            ScanWifi = true,
            ScanBluetooth = true,
            ScanBtLe = true,
            ScanClientsPromiscuous = true,
            BatchIntervalMs = 1500,
            Interfaces =
            [
                new InterfaceConfig { Name = "wlan0", Driver = "nl80211", MonitorMode = true, Enabled = true, Channels = [1, 6, 11, 36, 40, 44, 149, 153, 157] },
                new InterfaceConfig { Name = "hci0", Driver = "bluez", MonitorMode = false, Enabled = true },
            ],
        };
        var ble = new ScanConfig
        {
            ChannelHopMs = 1000,
            ScanWifi = false,
            ScanBluetooth = true,
            ScanBtLe = true,
            ScanClientsPromiscuous = false,
            BatchIntervalMs = 1500,
            Interfaces = [new InterfaceConfig { Name = "hci0", Driver = "bluez", MonitorMode = false, Enabled = true }],
        };
        var drone = new ScanConfig
        {
            ChannelHopMs = 500,
            ScanWifi = true,
            ScanBluetooth = false,
            ScanBtLe = true,
            ScanClientsPromiscuous = false,
            BatchIntervalMs = 1500,
            Interfaces =
            [
                new InterfaceConfig { Name = "wlan0", Driver = "nl80211", MonitorMode = true, Enabled = true },
                new InterfaceConfig { Name = "wlan1", Driver = "nl80211", MonitorMode = true, Enabled = true },
            ],
        };
        var deep = new ScanConfig
        {
            ChannelHopMs = 2000,
            ScanWifi = true,
            ScanBluetooth = false,
            ScanBtLe = true,
            ScanClientsPromiscuous = true,
            BatchIntervalMs = 2500,
            Interfaces = [new InterfaceConfig { Name = "wlan0", Driver = "nl80211", MonitorMode = true, Enabled = true, Channels = [1, 6, 11] }],
        };
        var airport = new ScanConfig
        {
            ChannelHopMs = 400,
            ScanWifi = true,
            ScanBluetooth = false,
            ScanBtLe = true,
            ScanClientsPromiscuous = false,
            BatchIntervalMs = 1500,
            Interfaces = [new InterfaceConfig { Name = "wlan0", Driver = "nl80211", MonitorMode = true, Enabled = true, Channels = [36, 40, 44, 48, 149, 153, 157, 161] }],
        };
        var rural = new ScanConfig
        {
            ChannelHopMs = 1500,
            ScanWifi = true,
            ScanBluetooth = false,
            ScanBtLe = true,
            ScanClientsPromiscuous = false,
            BatchIntervalMs = 5000,
            Interfaces = [new InterfaceConfig { Name = "wlan0", Driver = "nl80211", MonitorMode = true, Enabled = true, Channels = [1, 6, 11] }],
        };

        var presets = new (string Name, string? Description, ScanConfig Cfg, bool Builtin)[]
        {
            ("Off", "All radios off — new devices start here.", off, true),
            ("Urban Hopper", "WiFi + BLE hopping, fast dwell, for dense city ops.", urban, false),
            ("BLE Beacon Sweep", "BLE-focused sweep for asset-tag hunting.", ble, false),
            ("Drone Chase", "Two-card simultaneous capture for drone ops.", drone, false),
            ("Deep WiFi", "Long dwell per channel, clients included.", deep, false),
            ("Airport Sweep", "5GHz-leaning sweep for terminal surveys.", airport, false),
            ("Rural Low-Power", "Battery-friendly slow scan for rural coverage.", rural, false),
        };

        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < presets.Length; i++)
        {
            var (name, description, cfg, builtin) = presets[i];
            var createdAt = now.AddMinutes(-_rng.Int(5000, 40000));
            db.Presets.Add(new Preset
            {
                Name = name,
                Description = description,
                OwnerId = builtin ? null : operatorId,
                ConfigJson = ScanConfigParser.Stringify(cfg),
                IsBuiltin = builtin,
                CreatedAt = createdAt,
                UpdatedAt = now.AddMinutes(-_rng.Int(0, 5000)),
            });
        }
    }

    private void SeedFleet()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var name in DataGenerators.FleetNames)
        {
            var platform = _rng.Pick(DataGenerators.Platforms);
            var status = _rng.WeightedPick([(DeviceStatus.Online, 3), (DeviceStatus.Offline, 1), (DeviceStatus.Error, 1)]);
            db.Devices.Add(new Device
            {
                Name = name,
                Platform = platform,
                CapabilitiesJson = Mappers.CapabilitiesJson(DataGenerators.RandomCapabilities(_rng, platform)),
                Status = status,
                LastHeartbeatAt = now.AddMinutes(-_rng.Int(0, 45)),
                LastKnownIp = $"10.10.{_rng.Int(1, 9)}.{_rng.Int(2, 254)}",
                PairedAt = now.AddMinutes(-_rng.Int(1000, 30000)),
            });
        }
    }

    private List<Session> SeedSessions()
    {
        var now = DateTimeOffset.UtcNow;
        var sessions = new List<Session>();
        var cities = DataGenerators.Cities;
        var suffixes = new[] { "Downtown", "North", "Airport", "University", "Harbor", "Old Town", "Tech Park", "Central" };

        for (var i = 0; i < 8; i++)
        {
            var status = i < 4 ? SessionStatus.Active : i < 7 ? SessionStatus.Archived : SessionStatus.Planned;
            var city = cities[i];
            var (latMin, lonMin, latMax, lonMax) = DataGenerators.BoundingArea(city, 0.05);
            var startsAt = status == SessionStatus.Planned
                ? now.AddDays(_rng.Int(1, 7))
                : now.AddMinutes(-_rng.Int(60, 3000));
            var endsAt = status == SessionStatus.Archived
                ? now.AddMinutes(-_rng.Int(0, 2000))
                : status == SessionStatus.Planned ? now.AddDays(_rng.Int(8, 14)) : (DateTimeOffset?)null;

            sessions.Add(new Session
            {
                Name = $"{city.Name} {suffixes[i]} Sweep",
                Description = status == SessionStatus.Archived ? "Completed op — see export center for results." : $"Sensor coverage of the {city.Name}.",
                StartsAt = startsAt,
                EndsAt = endsAt,
                Status = status,
                LatMin = latMin,
                LonMin = lonMin,
                LatMax = latMax,
                LonMax = lonMax,
                CreatedAt = now.AddMinutes(-_rng.Int(3000, 40000)),
                UpdatedAt = now.AddMinutes(-_rng.Int(0, 2000)),
            });
        }

        db.Sessions.AddRange(sessions);
        return sessions;
    }

    private void SeedMemberships(List<Session> sessions, Guid operatorId)
    {
        var devices = db.Devices.Local.ToList();
        var now = DateTimeOffset.UtcNow;
        foreach (var session in sessions)
        {
            var isActive = session.Status == SessionStatus.Active;
            var pool = devices.Where(d => d.Status != DeviceStatus.Pending || isActive).ToList();
            var memberCount = isActive ? _rng.Int(5, 9) : _rng.Int(3, 6);
            var members = pool.OrderBy(_ => _rng.Next()).Take(Math.Min(memberCount, pool.Count)).ToList();
            var swarmCount = isActive ? Math.Min(3, Math.Max(2, members.Count / 3)) : 2;
            var swarms = Enumerable.Range(0, swarmCount)
                .Select(i => new Swarm { SessionId = session.Id, Name = new[] { "Alpha", "Bravo", "Charlie" }[i] })
                .ToList();
            db.Swarms.AddRange(swarms);

            var presets = db.Presets.Local.Skip(1).ToList();
            for (var i = 0; i < members.Count; i++)
            {
                var device = members[i];
                var swarm = swarms[Math.Min(i / 3, swarms.Count - 1)];
                var preset = _rng.Pick(presets);
                var cfg = ScanConfigParser.Parse(preset.ConfigJson);
                if (_rng.Next() > 0.3 && session.Status == SessionStatus.Active)
                    cfg.ChannelHopMs = Math.Max(100, cfg.ChannelHopMs + _rng.Int(-200, 200));

                db.SessionDevices.Add(new SessionDevice
                {
                    SessionId = session.Id,
                    DeviceId = device.Id,
                    SwarmId = swarm.Id,
                    Role = i % 4 == 0 ? "lead" : "scout",
                    JoinedAt = session.CreatedAt,
                    ConfigJson = ScanConfigParser.Stringify(cfg),
                    ConfigRev = _rng.Int(1, 8),
                    PresetId = preset.Id,
                    ConfigSource = ConfigSource.Preset,
                    PushState = PushState.Acked,
                    LastPushId = Guid.NewGuid().ToString("N"),
                    ConfigUpdatedAt = now.AddMinutes(-_rng.Int(1, 600)),
                });
            }
        }
        _ = operatorId;
    }

    private void SeedDetections(List<Session> sessions)
    {
        var now = DateTimeOffset.UtcNow;
        var memberDevices = db.SessionDevices.Local
            .GroupBy(sd => sd.SessionId)
            .ToDictionary(g => g.Key, g => g.Select(sd => sd.DeviceId).ToList());
        var allDevices = db.Devices.Local.ToDictionary(d => d.Id);

        foreach (var session in sessions)
        {
            var (latMin, lonMin, latMax, lonMax) =
                (session.LatMin, session.LonMin, session.LatMax, session.LonMax) is ({ } a, { } b, { } c, { } d) ? (a, b, c, d) : DataGenerators.BoundingArea(DataGenerators.Cities[0]);

            var perSession = session.Status == SessionStatus.Active ? _rng.Int(380, 520) : _rng.Int(120, 220);
            var regionDevices = Enumerable.Range(0, perSession).Select(_ =>
            {
                var (oui, vendor) = DataGenerators.OuiFor(_rng);
                var mac = DataGenerators.RandomMac(_rng, oui);
                return (Mac: mac, MacNorm: MacNormalizer.Normalize(mac), Oui: oui, Vendor: vendor, Type: DataGenerators.RandomDeviceType(_rng));
            }).ToList();

            var deviceIds = memberDevices.GetValueOrDefault(session.Id) ?? allDevices.Keys.Take(3).ToList();
            var detectorPool = deviceIds.Where(allDevices.ContainsKey).Select(id => allDevices[id]).ToList();
            if (detectorPool.Count == 0) detectorPool = allDevices.Values.Take(3).ToList();

            var swarmByDevice = db.SessionDevices.Local
                .Where(sd => sd.SessionId == session.Id)
                .ToDictionary(sd => sd.DeviceId, sd => sd.SwarmId);

            var detectionCount = session.Status == SessionStatus.Active ? _rng.Int(700, 1300) : _rng.Int(150, 400);
            var sessionStart = session.StartsAt ?? now.AddMinutes(-600);
            var spanMs = Math.Min(86_400_000.0, Math.Max(60_000.0, (now - sessionStart).TotalMilliseconds));

            for (var i = 0; i < detectionCount; i++)
            {
                var region = _rng.Pick(regionDevices);
                var detector = _rng.Pick(detectorPool);
                var roll = _rng.Next();
                var locationFlag = roll < 0.55 ? LocationFlag.Gps : roll < 0.8 ? LocationFlag.Inferred : LocationFlag.Unlocated;
                (double Lat, double Lon)? point = locationFlag == LocationFlag.Unlocated ? null : DataGenerators.PointInArea(_rng, latMin, lonMin, latMax, lonMax);
                var ssid = region.Type == DeviceType.Ap ? DataGenerators.RandomSsid(_rng) : region.Type == DeviceType.Client ? DataGenerators.RandomClientSsid(_rng) : null;
                var btName = region.Type is DeviceType.Ap or DeviceType.Client ? null : DataGenerators.RandomBtName(_rng);
                var detectedAt = sessionStart.AddMilliseconds(_rng.Float(0, spanMs));

                db.Detections.Add(new Detection
                {
                    BatchId = Guid.NewGuid().ToString("N"),
                    SessionId = session.Id,
                    DeviceId = detector.Id,
                    SwarmId = swarmByDevice.GetValueOrDefault(detector.Id),
                    DeviceType = region.Type,
                    Mac = region.Mac,
                    MacNormalized = region.MacNorm,
                    Ssid = ssid,
                    BtName = btName,
                    Channel = DataGenerators.ChannelFor(region.Type, _rng),
                    SignalDbm = DataGenerators.SignalFor(region.Type, _rng),
                    Encryption = region.Type == DeviceType.Ap ? DataGenerators.RandomEncryption(_rng) : Encryption.Unspecified,
                    DetectedAt = detectedAt,
                    LocationFlag = locationFlag,
                    Lat = point?.Lat,
                    Lon = point?.Lon,
                    Source = DetectionSource.Scanner,
                });
            }
        }
    }

    private void SeedExports(List<Session> sessions, Guid operatorId)
    {
        var now = DateTimeOffset.UtcNow;
        var formats = new[] { ExportFormat.WigleCsv, ExportFormat.Csv, ExportFormat.GeoJson };
        for (var i = 0; i < 26; i++)
        {
            var session = _rng.Next() > 0.35 ? _rng.Pick(sessions) : null;
            var format = _rng.Pick(formats);
            var status = _rng.WeightedPick([(ExportStatus.Done, 5), (ExportStatus.Failed, 1), (ExportStatus.Queued, 1), (ExportStatus.Running, 1)]);
            var fileName = status == ExportStatus.Done
                ? $"{(session?.Name ?? "all").Replace(" ", "-")}-{format}." + (format == ExportFormat.GeoJson ? "geojson" : "csv")
                : null;
            db.Exports.Add(new Export
            {
                SessionId = session?.Id,
                SessionName = session?.Name,
                Format = format,
                Status = status,
                FileName = fileName,
                CreatedAt = now.AddMinutes(-_rng.Int(20, 5000)),
                OwnerId = operatorId,
                SizeBytes = status == ExportStatus.Done ? _rng.Int(1000, 900000) : null,
                RowCount = status == ExportStatus.Done ? _rng.Int(50, 3000) : null,
            });
        }
    }

    private void SeedPairings()
    {
        var pendingCaps = new[]
        {
            new DeviceCapabilities { HasWifiMonitor = true, HasBluetooth = false, HasGps = true, HasBattery = false, Platform = "raspberry-pi" },
            new DeviceCapabilities { HasWifiMonitor = true, HasBluetooth = true, HasGps = true, HasBattery = true, Platform = "android-14" },
            new DeviceCapabilities { HasWifiMonitor = false, HasBluetooth = true, HasGps = false, HasBattery = false, Platform = "linux-arm64" },
            new DeviceCapabilities { HasWifiMonitor = true, HasBluetooth = false, HasGps = true, HasBattery = false, Platform = "linux-x64" },
            new DeviceCapabilities { HasWifiMonitor = true, HasBluetooth = true, HasGps = true, HasBattery = true, Platform = "android-13" },
            new DeviceCapabilities { HasWifiMonitor = true, HasBluetooth = true, HasGps = true, HasBattery = false, Platform = "raspberry-pi" },
        };
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < pendingCaps.Length; i++)
        {
            db.Pairings.Add(new Pairing
            {
                DeviceId = Guid.NewGuid(),
                DeviceName = $"Scanner-{i + 1}",
                Platform = pendingCaps[i].Platform,
                CapabilitiesJson = Mappers.CapabilitiesJson(pendingCaps[i]),
                Status = i < 4 ? PairingStatus.Pending : i == 4 ? PairingStatus.Approved : PairingStatus.Rejected,
                RequestedAt = now.AddMinutes(-_rng.Int(2, 600)),
            });
        }
    }
}

public interface IDbSeeder
{
    Task SeedAsync(CancellationToken ct);
}
