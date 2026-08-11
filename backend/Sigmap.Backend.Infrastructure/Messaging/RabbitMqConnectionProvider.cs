using Microsoft.Extensions.Logging;
using Sigmap.Contracts.Messaging;
using RabbitMQ.Client;

namespace Sigmap.Backend.Infrastructure.Messaging;

/// <summary>Lazily creates and caches the backend's RabbitMQ connection, with
/// automatic reconnection semantics handled by the client.</summary>
public sealed class RabbitMqConnectionProvider : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<RabbitMqConnectionProvider> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnectionProvider(string connectionString, ILogger<RabbitMqConnectionProvider> log)
    {
        _connectionString = connectionString;
        _log = log;
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _gate.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            var factory = new ConnectionFactory { Uri = new Uri(_connectionString) };
            var conn = await factory.CreateConnectionAsync("sigmap-backend", ct);
            conn.ConnectionShutdownAsync += (_, e) =>
            {
                _log.LogWarning("RabbitMQ connection shut down: {Reason}", e.ReplyText);
                return Task.CompletedTask;
            };
            _connection = conn;
            return conn;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Opens a short-lived channel with publisher confirmations enabled.</summary>
    public async Task<IChannel> CreatePublisherChannelAsync(CancellationToken ct)
    {
        var conn = await GetConnectionAsync(ct);
        var options = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        return await conn.CreateChannelAsync(options, ct);
    }

    /// <summary>Opens a short-lived channel for publishing single messages.</summary>
    public async Task PublishAsync(string exchange, string routingKey, ReadOnlyMemory<byte> body, bool persistent, CancellationToken ct)
    {
        await using var channel = await CreatePublisherChannelAsync(ct);
        // Idempotent: guarantees the exchange/queue topology exists before publish.
        await RabbitMqTopology.DeclareAsync(channel, ct);
        var props = new BasicProperties
        {
            DeliveryMode = persistent ? DeliveryModes.Persistent : DeliveryModes.Transient,
        };
        // On a confirm-enabled channel, awaiting this task completes only after
        // the broker confirms the publish (or the tracking times out).
        await channel.BasicPublishAsync(exchange, routingKey, true, props, body, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
        _gate.Dispose();
    }
}
