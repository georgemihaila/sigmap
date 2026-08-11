using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Sigmap.Bff;

/// <summary>Lazily cached RabbitMQ connection for the BFF's live-event consumer.</summary>
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
            var conn = await factory.CreateConnectionAsync("sigmap-bff", ct);
            conn.ConnectionShutdownAsync += (_, e) =>
            {
                _log.LogWarning("BFF RabbitMQ connection shut down: {Reason}", e.ReplyText);
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

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
        _gate.Dispose();
    }
}
