using Sigmap.Domain.Entities;
using Sigmap.Domain.Enums;

namespace Sigmap.Infrastructure.Simulation;

/// <summary>
/// Port of the frontend `src/mocks/generators.ts`: OUI table, SSID/BT names,
/// MACs, fleet names, cities, and signal/channel distributions.
/// </summary>
public static class DataGenerators
{
    public static readonly (string Oui, string Vendor)[] OuiTable =
    [
        ("00:1A:2B", "Intel Corporate"), ("00:1E:58", "Intel Corporate"), ("34:AA:8B", "Intel Corporate"),
        ("00:24:BE", "TP-Link Technologies"), ("5C:63:BF", "TP-Link Technologies"), ("5C:87:9C", "TP-Link Technologies"),
        ("00:50:F1", "ASUSTek Computer"), ("00:0C:29", "VMware"), ("00:23:24", "Dell"),
        ("00:18:4D", "Hewlett Packard"), ("00:1B:63", "Cisco-Linksys"), ("84:D8:1B", "Cisco Systems"),
        ("20:2B:3D", "Cisco Meraki"), ("00:22:2D", "Micro-Star International"), ("00:1C:AB", "Giga-Byte Technology"),
        ("00:23:8E", "Belkin International"), ("00:26:5A", "Samsung Electronics"), ("08:3E:8E", "Samsung Electronics"),
        ("48:8F:5A", "Samsung Electronics"), ("00:1F:5B", "Netgear"), ("B0:BE:76", "Netgear"),
        ("8C:3A:E3", "D-Link"), ("6C:0B:84", "Huawei Technologies"), ("70:4F:57", "Apple"),
        ("00:15:6D", "Apple"), ("00:0D:67", "Apple"), ("04:02:1F", "Amazon Technologies"),
        ("68:05:CA", "Amazon Technologies"), ("48:F8:B3", "Roku"), ("90:9A:4A", "Google"),
        ("B8:27:EB", "Raspberry Pi Foundation"), ("DC:A6:32", "Raspberry Pi Foundation"), ("00:1A:11", "Sony"),
        ("D8:32:14", "Espressif"), ("3C:6A:9D", "Espressif"), ("24:4B:FE", "Liteon Technology"),
        ("00:13:EF", "Sagemcom"), ("2C:30:33", "Wistron Neweb"), ("00:15:0F", "LG Electronics"),
        ("7C:11:BE", "LG Electronics"), ("44:6C:9D", "Liteon"), ("5C:02:14", "Motorola Mobility"),
        ("F4:81:39", "Xiaomi Communications"), ("00:5A:39", "Ubiquiti"), ("24:A4:3C", "Ubiquiti"),
        ("04:18:D6", "MikroTik"), ("24:0A:C4", "Acer"), ("00:1E:10", "Hewlett Packard"),
    ];

    private static readonly (string Base, int Weight)[] Residential =
    [
        ("HOME", 3), ("NETGEAR", 2), ("TP-LINK_", 2), ("MyWiFi", 2), ("FRITZ!Box", 2),
        ("BTHomeHub", 1), ("SKY", 1), ("O2-WLAN", 1), ("Speedtest", 1),
    ];

    private static readonly string[] Enterprise =
    ["eduroam", "eduroam - Radsec", "corp-5GHz", "corp-guest", "Starbucks WiFi", "Airport Free WiFi", "HotelGuest", "Uni-Guest", "xfinitywifi", "Vodafone Hotspot"];

    private static readonly string[] Iot =
    ["TP-Link_SmartPlug", "SmartLife", "Hue Bridge", "Sonos", "TuyaSmart", "Galaxy-", "esp-", "MiBand", "Ring-", "RingCam"];

    private static readonly string[] BtNames =
    [
        "AirPods Pro", "Galaxy Buds2", "Fitbit Versa 3", "JBL Flip 5", "MacBook Pro",
        "iPhone 15", "Samsung TV", "Xbox Series X", "PlayStation 5", "Fitbit Charge 5",
        "Tile Mate", "Beats Solo", "Pixel Buds", "Garmin Forerunner", "Withings Body+",
        "ESP32-BLE", "iTag", "Mi Band 6", "Logitech MX Master", "Bose QC45",
    ];

    private static readonly string[] ClientPrefixes = ["DESKTOP-", "LAPTOP-", "ANDROID-", "iPhone", "PC-"];

    public static readonly string[] FleetNames =
    [
        "Sierra-1", "Sierra-2", "Tango-1", "Foxtrot-3", "RPi-Beta", "RPi-Gamma",
        "Aurora-1", "Pixel-8", "Pixel-6a", "Galaxy-S23", "ThinkPad-X1", "MacBook-M2",
        "Nuc-5", "OrangePi-1", "RPi-Zero2", "Xiaomi-11", "Vega-2", "Nimbus-4",
    ];

    public static readonly string[] Platforms = ["linux-x64", "linux-arm64", "raspberry-pi", "android-14", "android-13"];

    public record City(string Name, double Lat, double Lon);

    public static readonly City[] Cities =
    [
        new("Munich", 48.1371, 11.5754),
        new("Berlin", 52.52, 13.405),
        new("Prague", 50.0755, 14.4378),
        new("Vienna", 48.2082, 16.3738),
        new("Zürich", 47.3769, 8.5417),
        new("Salzburg", 47.8095, 13.055),
        new("Stuttgart", 48.7758, 9.1829),
        new("Freiburg", 47.999, 7.8421),
    ];

    public static (string Oui, string Vendor) OuiFor(SimRng rng) => rng.Pick(OuiTable);

    public static string RandomMac(SimRng rng, string? oui = null)
    {
        var prefix = (oui ?? OuiFor(rng).Oui).Split(':').ToList();
        while (prefix.Count < 6)
            prefix.Add(rng.Int(0, 255).ToString("X2"));
        return string.Join(':', prefix);
    }

    public static string RandomSsid(SimRng rng)
    {
        var style = rng.WeightedPick([("residential", 10), ("enterprise", 3), ("iot", 2)]);
        if (style == "enterprise") return rng.Pick(Enterprise);
        if (style == "iot")
        {
            var baseName = rng.Pick(Iot);
            return baseName + (rng.Next() > 0.5 ? rng.Int(10, 999).ToString() : "");
        }
        var template = rng.WeightedPick(Residential);
        return template + rng.Int(1000, 9999);
    }

    public static string RandomBtName(SimRng rng) => rng.Pick(BtNames);

    public static string? RandomClientSsid(SimRng rng) => rng.Next() > 0.3 ? null : rng.Pick(ClientPrefixes);

    public static DeviceCapabilities RandomCapabilities(SimRng rng, string platform) => new()
    {
        HasWifiMonitor = rng.Next() > 0.25,
        HasBluetooth = rng.Next() > 0.35,
        HasGps = rng.Next() > 0.2,
        HasBattery = platform.StartsWith("android"),
        Platform = platform,
    };

    public static (double LatMin, double LonMin, double LatMax, double LonMax) BoundingArea(City city, double radiusDeg = 0.045) => (
        city.Lat - radiusDeg,
        city.Lon - radiusDeg * 1.6,
        city.Lat + radiusDeg,
        city.Lon + radiusDeg * 1.6);

    public static (double Lat, double Lon) PointInArea(SimRng rng, double latMin, double lonMin, double latMax, double lonMax) =>
        (rng.Float(latMin, latMax), rng.Float(lonMin, lonMax));

    public static int SignalFor(DeviceType type, SimRng rng) => type switch
    {
        DeviceType.Ap => -rng.Int(28, 92),
        DeviceType.Client => -rng.Int(35, 88),
        _ => -rng.Int(40, 90),
    };

    public static int ChannelFor(DeviceType type, SimRng rng) => type switch
    {
        DeviceType.Ap or DeviceType.Client => rng.Pick(new[] { 1, 6, 11, 36, 40, 44, 48, 149, 153, 157, 161 }),
        DeviceType.Bluetooth => rng.Int(0, 39),
        _ => rng.Int(37, 39),
    };

    public static Encryption RandomEncryption(SimRng rng) => rng.WeightedPick(
    [
        (Encryption.Open, 15), (Encryption.Wep, 5), (Encryption.Wpa, 10), (Encryption.Wpa2, 60), (Encryption.Wpa3, 10),
    ]);

    public static DeviceType RandomDeviceType(SimRng rng) => rng.WeightedPick(
    [
        (DeviceType.Ap, 58), (DeviceType.Bluetooth, 14), (DeviceType.BtLe, 18), (DeviceType.Client, 10),
    ]);
}
