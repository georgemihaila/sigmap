using Sigmap.Contracts.Proto;
using Sigmap.Contracts.Proto.Live;

namespace Sigmap.Contracts.Messages;

/// <summary>Builds versioned envelopes. Shared by backend and scanner so the
/// wire format cannot drift.</summary>
public static class EnvelopeFactory
{
    public static Envelope For(string messageId, DetectionBatch batch) => new()
    {
        MessageId = messageId,
        SchemaVersion = Schema.Version,
        SentAtUnixMs = Now(),
        DetectionBatch = batch,
    };

    public static Envelope For(string messageId, ConfigPush push) => new()
    {
        MessageId = messageId,
        SchemaVersion = Schema.Version,
        SentAtUnixMs = Now(),
        ConfigPush = push,
    };

    public static Envelope For(string messageId, ConfigAck ack) => new()
    {
        MessageId = messageId,
        SchemaVersion = Schema.Version,
        SentAtUnixMs = Now(),
        ConfigAck = ack,
    };

    public static Envelope For(string messageId, Heartbeat heartbeat) => new()
    {
        MessageId = messageId,
        SchemaVersion = Schema.Version,
        SentAtUnixMs = Now(),
        Heartbeat = heartbeat,
    };

    public static Envelope For(string messageId, PairingRequest request) => new()
    {
        MessageId = messageId,
        SchemaVersion = Schema.Version,
        SentAtUnixMs = Now(),
        PairingRequest = request,
    };

    public static Envelope For(string messageId, PairingResponse response) => new()
    {
        MessageId = messageId,
        SchemaVersion = Schema.Version,
        SentAtUnixMs = Now(),
        PairingResponse = response,
    };

    public static Envelope For(string messageId, LiveEvent liveEvent) => new()
    {
        MessageId = messageId,
        SchemaVersion = Schema.Version,
        SentAtUnixMs = Now(),
        LiveEvent = liveEvent,
    };

    public static ulong Now() => (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
