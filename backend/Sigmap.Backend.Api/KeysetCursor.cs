using System.Text;
using System.Text.Json;

namespace Sigmap.Backend.Api;

/// <summary>Keyset/cursor pagination helper. Encodes (sort key, id) as a compact
/// base64 JSON token so list endpoints page stably under inserts.</summary>
public static class KeysetCursor
{
    public static string Encode(DateTimeOffset sortKey, string id)
    {
        var payload = JsonSerializer.Serialize(new CursorPayload { K = sortKey.UtcTicks, I = id });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static CursorPayload? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return null;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return JsonSerializer.Deserialize<CursorPayload>(json);
        }
        catch
        {
            return null;
        }
    }

    public sealed class CursorPayload
    {
        public long K { get; set; }
        public string I { get; set; } = string.Empty;
    }
}
