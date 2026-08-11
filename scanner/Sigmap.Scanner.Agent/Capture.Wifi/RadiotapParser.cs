namespace Sigmap.Scanner.Agent.Capture.Wifi;

/// <summary>Extracts signal/channel from a radiotap header (IEEE 802.11 radio
/// tap). Fields: present bitmap word 0 bits — 0x4 = channel, 0x20 = dBm antenna
/// signal.</summary>
public static class RadiotapParser
{
    public const uint BitmapChannel = 1u << 2;
    public const uint BitmapDbmAntSignal = 1u << 5;

    public readonly record struct Result(int? SignalDbm, int? Channel, int HeaderLength);

    public static Result Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8)
            return new Result(null, null, 0);

        var version = data[0];
        if (version != 0)
            return new Result(null, null, 0);

        var headerLength = data[2] | (data[3] << 8);
        if (headerLength < 8 || headerLength > data.Length)
            return new Result(null, null, 0);

        var present0 = BitConverter.ToUInt32(data[4..8]);
        int? signal = null;
        int? channel = null;

        // v1: single 32-bit present word (nested/vendor fields ignored).
        var offset = 8;
        if ((present0 & BitmapChannel) != 0 && offset + 4 <= headerLength)
        {
            var freq = BitConverter.ToUInt16(data[offset..]);
            channel = FreqToChannel(freq);
            offset += 4;
        }

        if ((present0 & BitmapDbmAntSignal) != 0 && offset + 1 <= headerLength)
        {
            signal = (sbyte)data[offset];
        }

        return new Result(signal, channel, headerLength);
    }

    /// <summary>Converts a 802.11 frequency (MHz) to the standard channel number,
    /// returning null for frequencies we don't recognize.</summary>
    public static int? FreqToChannel(int freqMhz)
    {
        if (freqMhz >= 2412 && freqMhz <= 2484)
            return (freqMhz - 2412) / 5 + 1;
        if (freqMhz >= 5180 && freqMhz <= 5905)
            return (freqMhz - 5180) / 5 + 36;
        return null;
    }
}
