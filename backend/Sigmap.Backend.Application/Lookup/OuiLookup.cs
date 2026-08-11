namespace Sigmap.Backend.Application.Lookup;

public readonly record struct OuiInfo(string Oui, string Name);

/// <summary>
/// IEEE OUI vendor lookup. Ships with a small curated set of common vendors so
/// the pipeline is useful out of the box; swap in the full OUI file
/// (oui.csv) behind this interface for production coverage.
/// </summary>
public sealed class OuiLookup
{
    private readonly Dictionary<string, string> _byOui = new(StringComparer.OrdinalIgnoreCase);

    public OuiLookup()
    {
        Add("00:0C:42", "Ubiquiti");
        Add("00:13:10", "Samsung");
        Add("00:16:EA", "Sonos");
        Add("00:17:C8", "Kilby");
        Add("00:1A:3F", "Intel");
        Add("00:1F:3B", "Apple");
        Add("00:1F:4B", "Apple");
        Add("00:21:5C", "Apple");
        Add("00:23:DF", "Apple");
        Add("00:25:00", "Apple");
        Add("00:26:BB", "Apple");
        Add("00:26:BB", "Apple");
        Add("00:3A:9C", "Apple");
        Add("00:3E:E1", "Apple");
        Add("00:04:ED", "Raspberry Pi");
        Add("B8:27:EB", "Raspberry Pi");
        Add("DC:A6:32", "Raspberry Pi");
        Add("E4:5F:01", "Raspberry Pi");
        Add("00:13:CE", "Hewlett Packard");
        Add("00:14:22", "Dell");
        Add("00:17:A4", "Dell");
        Add("00:1B:21", "Intel");
        Add("00:1B:63", "Intel");
        Add("00:1E:4C", "Intel");
        Add("00:21:6A", "Intel");
        Add("00:50:56", "VMware");
        Add("00:0C:29", "VMware");
        Add("00:11:32", "D-Link");
        Add("00:22:B0", "D-Link");
        Add("00:24:5B", "D-Link");
        Add("00:1A:E9", "D-Link");
        Add("00:21:91", "D-Link");
        Add("00:14:BF", "TP-Link");
        Add("00:1A:2B", "TP-Link");
        Add("00:25:86", "TP-Link");
        Add("50:C7:BF", "TP-Link");
        Add("D8:07:B6", "TP-Link");
        Add("EC:08:6B", "TP-Link");
        Add("F4:F2:6D", "TP-Link");
        Add("00:0C:41", "Cisco-Linksys");
        Add("00:14:6C", "Cisco-Linksys");
        Add("00:16:B6", "Cisco-Linksys");
        Add("00:22:6B", "Cisco-Linksys");
        Add("00:1B:0C", "Cisco");
        Add("00:1B:53", "Cisco");
        Add("00:18:0A", "Cisco");
        Add("00:21:1C", "Cisco");
        Add("00:26:0A", "Cisco");
        Add("08:00:27", "PCS Systemtechnik (VirtualBox)");
        Add("00:15:5D", "Microsoft (Hyper-V)");
        Add("00:50:7B", "Realtek");
        Add("00:E0:4C", "Realtek");
        Add("00:1F:1F", "Qualcomm");
        Add("F8:75:A4", "Qualcomm");
        Add("00:0A:F5", "Samsung");
        Add("00:16:38", "Samsung");
        Add("00:1A:22", "Samsung");
        Add("00:1C:48", "Samsung");
        Add("00:21:19", "Samsung");
        Add("78:0C:B8", "Samsung");
        Add("00:1C:B3", "Sony");
        Add("00:21:BD", "Sony");
        Add("00:23:E9", "Sony");
        Add("00:1F:5A", "Nintendo");
        Add("00:17:AB", "Google");
        Add("18:9E:FC", "Google");
        Add("3C:5A:B4", "Google");
        Add("8C:DE:F9", "Google");
        Add("00:1A:7D", "Xiaomi");
        Add("18:C5:8A", "Xiaomi");
        Add("00:16:3E", "Xerox");
        Add("00:0C:F1", "Aruba Networks");
        Add("00:1B:9E", "Aruba Networks");
        Add("00:1A:1E", "Zyxel");
        Add("00:23:CD", "ASUS");
        Add("04:D9:F5", "ASUS");
        Add("00:24:2C", "Juniper");
        Add("00:15:3C", "Ericsson");
        Add("00:23:6A", "Huawei");
        Add("48:8F:5A", "Huawei");
        Add("00:01:30", "Motorola");
        Add("00:0A:F6", "Motorola");
        Add("00:18:E7", "LG");
        Add("00:21:4E", "LG");
        Add("00:24:2B", "Belkin");
        Add("F4:5C:89", "Belkin");
        Add("00:0D:8C", "Acer");
        Add("00:1C:BF", "Netgear");
        Add("20:E5:2A", "Netgear");
        Add("00:14:6C", "Netgear");
        Add("00:1B:2F", "Netgear");
        Add("C4:3C:6C", "Broadcom");
        Add("00:1F:5B", "Nest");
        Add("00:23:34", "Amazon");
        Add("48:AD:08", "Amazon");
        Add("74:C2:46", "Amazon");
        Add("A0:02:DC", "Amazon");
        Add("FC:65:DE", "Amazon");
        Add("00:04:4B", "NVIDIA");
        Add("24:81:C7", "Espressif");
        Add("30:AE:A4", "Espressif");
        Add("5C:CF:7F", "Espressif");
        Add("A4:CF:12", "Espressif");
        Add("18:FE:34", "Espressif");
        Add("24:0A:C4", "Espressif");
        Add("24:62:AB", "Samsung");
        Add("24:FD:52", "Samsung");
        Add("60:A4:B7", "Samsung");
        Add("84:EB:18", "Samsung");
    }

    private void Add(string oui, string name) => _byOui.TryAdd(oui.ToUpperInvariant(), name);

    /// <summary>Looks up a MAC address (any common separator format).</summary>
    public bool TryLookup(string mac, out OuiInfo info)
    {
        var norm = NormalizeMac(mac);
        if (norm.Length < 6)
        {
            info = default;
            return false;
        }

        var oui = string.Join(":", Enumerable.Range(0, 3).Select(i => norm.Substring(i * 2, 2)));
        if (_byOui.TryGetValue(oui, out var name))
        {
            info = new OuiInfo(oui, name);
            return true;
        }

        info = new OuiInfo(oui, "Unknown");
        return false;
    }

    public static string NormalizeMac(string mac) =>
        new(mac.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
