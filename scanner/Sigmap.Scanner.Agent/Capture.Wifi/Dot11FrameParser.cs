using System.Text;
using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent.Capture.Wifi;

/// <summary>
/// Minimal 802.11 management-frame parser sufficient for wardriving: decodes
/// beacon / probe-request / probe-response frames to (BSSID, SSID). Parsing is
/// defensive — malformed frames yield null rather than throwing.
/// </summary>
public static class Dot11FrameParser
{
    public const byte TypeManagement = 0x00;

    public enum FrameKind
    {
        None,
        Beacon,
        ProbeRequest,
        ProbeResponse,
    }

    public readonly record struct Frame(FrameKind Kind, string Bssid, string? Ssid);

    /// <param name="frame">802.11 frame body (after radiotap header).</param>
    public static Frame Parse(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 24)
            return new Frame(FrameKind.None, string.Empty, null);

        var frameControl = frame[0];
        var type = (frameControl >> 2) & 0x03;
        var subtype = (frameControl >> 4) & 0x0F;
        if (type != TypeManagement)
            return new Frame(FrameKind.None, string.Empty, null);

        var bssid = ParseMac(frame[16..22]);

        // Beacon (0x8) and probe responses carry an SSID IE in the body at
        // offset 24. Probe requests (0x4) have the SSID IE too but in a
        // different position — parse element IDs defensively.
        var kind = subtype switch
        {
            0x8 => FrameKind.Beacon,
            0x4 => FrameKind.ProbeRequest,
            0x5 => FrameKind.ProbeResponse,
            _ => FrameKind.None,
        };
        if (kind == FrameKind.None)
            return new Frame(FrameKind.None, bssid, null);

        var ssid = ParseSsid(frame[24..]);
        return new Frame(kind, bssid, ssid);
    }

    /// <summary>Walks the tagged parameters looking for the SSID element (id 0).</summary>
    public static string? ParseSsid(ReadOnlySpan<byte> body)
    {
        var offset = 0;
        while (offset + 2 <= body.Length)
        {
            var id = body[offset];
            var len = body[offset + 1];
            var value = body.Slice(offset + 2, len);
            if (id == 0)
                return Encoding.UTF8.GetString(value).TrimEnd('\0');
            offset += 2 + len;
            if (offset > body.Length)
                break;
        }

        return null;
    }

    private static string ParseMac(ReadOnlySpan<byte> mac) =>
        string.Join(":", mac.ToArray().Select(b => b.ToString("X2")));
}
