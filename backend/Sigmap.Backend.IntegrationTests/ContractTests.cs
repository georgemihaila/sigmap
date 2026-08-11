using Google.Protobuf;
using Sigmap.Backend.Application.Configuration;
using Sigmap.Contracts;
using Sigmap.Contracts.Messages;
using Sigmap.Contracts.Proto;
using Sigmap.Contracts.Proto.Live;

namespace Sigmap.Backend.IntegrationTests;

/// <summary>
/// Contract tests for the message schemas. Backend, scanner and BFF share the
/// same generated Contracts assembly, so these guard the wire contract (schema
/// version, payload round-trips, oneof routing) rather than mocking it away.
/// </summary>
public class ContractTests
{
    [Fact]
    public void Schema_version_is_stable()
    {
        Assert.Equal("1", Schema.Version);
    }

    [Fact]
    public void Detection_batch_envelope_round_trips()
    {
        var batch = new DetectionBatch
        {
            DeviceId = "dev-1",
            BatchId = "batch-1",
            HasGps = true,
        };
        batch.GpsSamples.Add(new GpsSample { Lat = 52.0, Lon = 13.0, AccuracyM = 5, AtUnixMs = 1000 });
        batch.Detections.Add(new Detection
        {
            DeviceType = DeviceType.Ap,
            Mac = "AA:BB:CC:DD:EE:FF",
            Ssid = "Net",
            SignalDbm = -60,
            Channel = 6,
            Encryption = Encryption.Wpa2,
            DetectedAtUnixMs = 1000,
        });

        var envelope = EnvelopeFactory.For(batch.BatchId, batch);
        var parsed = Envelope.Parser.ParseFrom(envelope.ToByteArray());

        Assert.Equal(Schema.Version, parsed.SchemaVersion);
        Assert.Equal("batch-1", parsed.MessageId);
        Assert.Equal(Envelope.PayloadOneofCase.DetectionBatch, parsed.PayloadCase);
        Assert.Equal(batch.BatchId, parsed.DetectionBatch.BatchId);
        Assert.Single(parsed.DetectionBatch.GpsSamples);
        Assert.Equal("AA:BB:CC:DD:EE:FF", parsed.DetectionBatch.Detections[0].Mac);
    }

    [Fact]
    public void Config_push_ack_round_trips()
    {
        var push = new ConfigPush
        {
            PushId = "push-1",
            DeviceId = "dev-1",
            SessionId = "session-1",
            Config = new ScanConfig
            {
                ChannelHopMs = 500,
                ScanWifi = true,
                BatchIntervalMs = 1500,
            },
        };
        var ack = new ConfigAck
        {
            PushId = "push-1",
            DeviceId = "dev-1",
            Status = ConfigAckStatus.Applied,
        };

        var parsedPush = Envelope.Parser.ParseFrom(EnvelopeFactory.For(push.PushId, push).ToByteArray());
        var parsedAck = Envelope.Parser.ParseFrom(EnvelopeFactory.For(ack.PushId, ack).ToByteArray());

        Assert.Equal(Envelope.PayloadOneofCase.ConfigPush, parsedPush.PayloadCase);
        Assert.True(parsedPush.ConfigPush.Config.ScanWifi);
        Assert.Equal(Envelope.PayloadOneofCase.ConfigAck, parsedAck.PayloadCase);
        Assert.Equal(ConfigAckStatus.Applied, parsedAck.ConfigAck.Status);
    }

    [Fact]
    public void Heartbeat_and_live_event_round_trips()
    {
        var heartbeat = new Heartbeat
        {
            DeviceId = "dev-1",
            AtUnixMs = 1234,
            Status = DeviceStatus.Busy,
            DetectionsBuffered = 42,
        };
        var parsedHb = Envelope.Parser.ParseFrom(EnvelopeFactory.For("hb", heartbeat).ToByteArray());
        Assert.Equal(Envelope.PayloadOneofCase.Heartbeat, parsedHb.PayloadCase);
        Assert.Equal(42UL, parsedHb.Heartbeat.DetectionsBuffered);

        var located = new LocatedBatch { BatchId = "b", DeviceId = "d", SessionId = "s" };
        var live = new LiveEvent { Detections = located };
        var parsedLive = Envelope.Parser.ParseFrom(EnvelopeFactory.For("evt", live).ToByteArray());
        Assert.Equal(Envelope.PayloadOneofCase.LiveEvent, parsedLive.PayloadCase);
        Assert.Equal("b", parsedLive.LiveEvent.Detections.BatchId);
    }

    [Fact]
    public void Off_default_config_has_nothing_enabled()
    {
        var off = ScanConfigFactory.Off();
        Assert.False(off.ScanWifi);
        Assert.False(off.ScanBluetooth);
        Assert.False(off.ScanBtLe);
        Assert.False(off.ScanClientsPromiscuous);
        Assert.Empty(off.Interfaces);

        var roundTrip = ScanConfigFactory.FromJson(ScanConfigFactory.ToJson(off));
        Assert.Equal(off, roundTrip);
    }
}
