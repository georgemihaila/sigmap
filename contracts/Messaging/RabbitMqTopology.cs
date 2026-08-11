using RabbitMQ.Client;

namespace Sigmap.Contracts.Messaging;

/// <summary>
/// Shared broker topology: exchange/queue names and idempotent declaration.
/// Lives in Contracts so the backend and scanner agent cannot drift.
/// </summary>
public static class RabbitMqTopology
{
    public const string IngestExchange = "sigmap.ingest";
    public const string ConfigExchange = "sigmap.config";
    public const string HeartbeatExchange = "sigmap.heartbeat";
    public const string EventsExchange = "sigmap.events";
    public const string DlxExchange = "sigmap.dlx";

    public const string DetectionBatchQueue = "ingest.detection_batches";
    public const string DetectionBatchKeyPrefix = "detection.batch.";
    public const string ConfigAckQueue = "ingest.config_acks";
    public const string ConfigAckKeyPrefix = "config.ack.";
    public const string HeartbeatQueue = "heartbeat.all";
    public const string HeartbeatKeyPrefix = "heartbeat.";
    public const string ConfigQueuePrefix = "config.push.";
    public const string ConfigKeyPrefix = "config.push.";
    public const string BffQueue = "bff.live";
    public const string EventKeyPrefix = "event.";

    public static string ConfigQueue(string deviceId) => ConfigQueuePrefix + deviceId;
    public static string ConfigKey(string deviceId) => ConfigKeyPrefix + deviceId;

    /// <summary>Idempotent declaration of all exchanges/queues/bindings.</summary>
    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        var dlxArgs = new Dictionary<string, object?> { ["x-dead-letter-exchange"] = DlxExchange };

        await channel.ExchangeDeclareAsync(IngestExchange, "topic", true, false, null, false, false, ct);
        await channel.ExchangeDeclareAsync(ConfigExchange, "topic", true, false, null, false, false, ct);
        await channel.ExchangeDeclareAsync(HeartbeatExchange, "topic", true, false, null, false, false, ct);
        await channel.ExchangeDeclareAsync(EventsExchange, "topic", true, false, null, false, false, ct);
        await channel.ExchangeDeclareAsync(DlxExchange, "topic", true, false, null, false, false, ct);

        await channel.QueueDeclareAsync(DetectionBatchQueue, true, false, false, dlxArgs, false, false, ct);
        await channel.QueueBindAsync(DetectionBatchQueue, IngestExchange, DetectionBatchKeyPrefix + "*", null, false, ct);

        await channel.QueueDeclareAsync(ConfigAckQueue, true, false, false, null, false, false, ct);
        await channel.QueueBindAsync(ConfigAckQueue, IngestExchange, ConfigAckKeyPrefix + "*", null, false, ct);

        await channel.QueueDeclareAsync(HeartbeatQueue, true, false, false, null, false, false, ct);
        await channel.QueueBindAsync(HeartbeatQueue, HeartbeatExchange, HeartbeatKeyPrefix + "*", null, false, ct);

        await channel.QueueDeclareAsync(BffQueue, true, false, false, null, false, false, ct);
        await channel.QueueBindAsync(BffQueue, EventsExchange, EventKeyPrefix + "#", null, false, ct);

        await channel.QueueDeclareAsync(DetectionBatchQueue + ".dlq", true, false, false, null, false, false, ct);
        await channel.QueueBindAsync(DetectionBatchQueue + ".dlq", DlxExchange, DetectionBatchQueue, null, false, ct);
    }
}
