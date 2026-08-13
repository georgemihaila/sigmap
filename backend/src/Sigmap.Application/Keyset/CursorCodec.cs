namespace Sigmap.Application.Keyset;

/// <summary>
/// Keyset (cursor) pagination contract shared with the frontend
/// (`src/lib/cursor.ts`): the cursor is base64 of `{"sortKey": "...", "id": "..."}`
/// and the page starts strictly AFTER that tuple under DESC ordering.
/// </summary>
public sealed record Cursor(string SortKey, string Id);

public static class CursorCodec
{
    public static string Encode(Cursor cursor) => Base64Encode(System.Text.Json.JsonSerializer.Serialize(cursor));

    public static Cursor? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var json = Base64Decode(cursor);
            var parsed = System.Text.Json.JsonSerializer.Deserialize<Cursor>(json);
            if (parsed is null || string.IsNullOrEmpty(parsed.SortKey) || string.IsNullOrEmpty(parsed.Id)) return null;
            return parsed;
        }
        catch
        {
            return null;
        }
    }

    private static string Base64Encode(string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        return System.Convert.ToBase64String(bytes);
    }

    private static string Base64Decode(string s)
    {
        var bytes = System.Convert.FromBase64String(s);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
