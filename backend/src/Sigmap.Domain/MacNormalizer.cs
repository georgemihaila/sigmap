namespace Sigmap.Domain;

public static class MacNormalizer
{
    public static string Normalize(string mac) =>
        mac.Replace(":", string.Empty).Replace("-", string.Empty).Replace(".", string.Empty).ToLowerInvariant();

    public static string Canonical(string mac)
    {
        var hex = Normalize(mac);
        if (hex.Length != 12) return mac.ToUpperInvariant();
        return string.Join(':', Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2))).ToUpperInvariant();
    }
}
