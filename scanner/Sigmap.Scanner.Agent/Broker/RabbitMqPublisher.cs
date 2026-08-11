using Google.Protobuf;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Sigmap.Scanner.Agent.Buffer;
using Sigmap.Contracts.Messaging;
using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent.Broker;

/// <summary>
/// Publishes detection batches to the ingest exchange with publisher
/// confirms. A batch is considered delivered only when the broker confirms it;
/// the caller persists it offline first otherwise.
/// </summary>
public sealed class RabbitMqPublisher : IDetectionPublisher
{
    public string ConnectionString => _connectionString;
    private readonly string _connectionString;
    private readonly ILogger<RabbitMqPublisher> _log;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqPublisher(string connectionString, ILogger<RabbitMqPublisher> log)
    {
        _connectionString = connectionString;
        _log = log;
    }

    public async Task PublishAsync(Envelope envelope, CancellationToken ct)
    {
        var batch = envelope.DetectionBatch;
        await PublishAsync(RabbitMqTopology.IngestExchange, RabbitMqTopology.DetectionBatchKeyPrefix + batch.DeviceId, envelope, persistent: true, ct);
        _log.LogDebug("Published batch {BatchId} (confirmed)", batch.BatchId);
    }

    /// <summary>Generalized publish with broker confirmation.</summary>
    public async Task PublishAsync(string exchange, string routingKey, Envelope envelope, bool persistent, CancellationToken ct)
    {
        var conn = await GetConnectionAsync(ct);
        var options = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        await using var channel = await conn.CreateChannelAsync(options, ct);
        await RabbitMqTopology.DeclareAsync(channel, ct);

        var props = new BasicProperties { DeliveryMode = persistent ? DeliveryModes.Persistent : DeliveryModes.Transient };
        await channel.BasicPublishAsync(exchange, routingKey, true, props, envelope.ToByteArray(), ct);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _lock.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;
            var factory = new ConnectionFactory { Uri = new Uri(_connectionString) };
            _connection = await factory.CreateConnectionAsync("sigmap-scanner", ct);
            _connection.ConnectionShutdownAsync += (_, e) =>
            {
                _log.LogWarning("Scanner connection shut down: {Reason}", e.ReplyText);
                return Task.CompletedTask;
            };
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_connection is not null)
            return _connection.DisposeAsync();
        return ValueTask.CompletedTask;
    }
}
