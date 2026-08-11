using System.Text;
using Sigmap.Scanner.Agent.Capture.Wifi;

namespace Sigmap.Scanner.Tests;

public class RadiotapParserTests
{
    private static byte[] BuildRadiotap(uint present, byte[] fields)
    {
        var headerLen = 8 + fields.Length;
        var header = new byte[headerLen];
        header[0] = 0;              // version
        header[1] = 0;              // pad
        header[2] = (byte)headerLen;
        header[3] = (byte)(headerLen >> 8);
        BitConverter.GetBytes(present).CopyTo(header, 4);
        fields.CopyTo(header, 8);
        return header;
    }

    [Fact]
    public void Parses_dbm_signal_and_channel()
    {
        var fields = new byte[5];
        // channel field: freq (2) + flags (2)
        BitConverter.GetBytes((ushort)2412).CopyTo(fields, 0);
        fields[4] = (byte)0xB8;    // antenna signal: -72 dBm as two's complement

        var result = RadiotapParser.Parse(BuildRadiotap(
            RadiotapParser.BitmapChannel | RadiotapParser.BitmapDbmAntSignal, fields));

        Assert.Equal(1, result.Channel);   // 2412 MHz => ch 1
        Assert.Equal(-72, result.SignalDbm);
    }

    [Fact]
    public void Too_short_input_is_ignored()
    {
        var result = RadiotapParser.Parse(new byte[] { 0, 0 });
        Assert.Null(result.SignalDbm);
        Assert.Null(result.Channel);
        Assert.Equal(0, result.HeaderLength);
    }

    [Fact]
    public void Unknown_frequency_yields_no_channel()
    {
        var result = RadiotapParser.Parse(BuildRadiotap(RadiotapParser.BitmapChannel, new byte[4]));
        Assert.Null(result.Channel);
    }

    [Theory]
    [InlineData(2412, 1)]
    [InlineData(2437, 6)]
    [InlineData(2462, 11)]
    [InlineData(5180, 36)]
    [InlineData(5905, 181)]
    public void Freq_to_channel_mapping(int freq, int expected)
    {
        Assert.Equal(expected, RadiotapParser.FreqToChannel(freq));
    }
}

public class Dot11FrameParserTests
{
    [Fact]
    public void Parses_beacon_with_ssid()
    {
        var body = new byte[24];
        body[0] = 0x80; // type mgmt (0), subtype beacon (8) => frame control byte = (8<<4)|0 = 0x80
        // BSSID at bytes 16..22
        new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0x01 }.CopyTo(body, 16);

        var tags = new List<byte>();
        tags.AddRange(new byte[] { 0, 5 });          // SSID IE, len 5
        tags.AddRange(Encoding.UTF8.GetBytes("MyNet"));
        tags.AddRange(new byte[] { 1, 1, 1 });        // supported rates

        var frame = Dot11FrameParser.Parse(body.Concat(tags).ToArray());

        Assert.Equal(Dot11FrameParser.FrameKind.Beacon, frame.Kind);
        Assert.Equal("AA:BB:CC:DD:EE:01", frame.Bssid);
        Assert.Equal("MyNet", frame.Ssid);
    }

    [Fact]
    public void Non_management_frames_are_ignored()
    {
        var body = new byte[30];
        body[0] = 0x08; // data frame
        var frame = Dot11FrameParser.Parse(body);
        Assert.Equal(Dot11FrameParser.FrameKind.None, frame.Kind);
    }

    [Fact]
    public void Short_frames_are_ignored()
    {
        var frame = Dot11FrameParser.Parse(new byte[10]);
        Assert.Equal(Dot11FrameParser.FrameKind.None, frame.Kind);
    }
}
