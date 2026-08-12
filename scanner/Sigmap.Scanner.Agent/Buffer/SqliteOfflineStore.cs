using Google.Protobuf;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent.Buffer;

/// <summary>SQLite-backed offline buffer. Survives process crashes while the
/// broker is unreachable.</summary>
public sealed class SqliteOfflineStore : IOfflineStore, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ILogger<SqliteOfflineStore> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SqliteOfflineStore(string dbPath, ILogger<SqliteOfflineStore> log)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        _log = log;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS pending_batches (
                batch_id TEXT PRIMARY KEY,
                payload BLOB NOT NULL,
                queued_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_pending_queued_at ON pending_batches(queued_at);
            CREATE TABLE IF NOT EXISTS app_config (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                config_json TEXT NOT NULL,
                updated_at INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task PersistAsync(Envelope envelope, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var batch = envelope.DetectionBatch;
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO pending_batches (batch_id, payload, queued_at)
                VALUES ($batch, $payload, $queued)
                ON CONFLICT(batch_id) DO NOTHING
                """;
            cmd.Parameters.AddWithValue("$batch", batch.BatchId);
            cmd.Parameters.AddWithValue("$payload", envelope.ToByteArray());
            cmd.Parameters.AddWithValue("$queued", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<Envelope>> PeekOldestAsync(int max, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var result = new List<Envelope>();
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT payload FROM pending_batches ORDER BY queued_at LIMIT $max
                """;
            cmd.Parameters.AddWithValue("$max", max);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                result.Add(Envelope.Parser.ParseFrom((byte[])reader["payload"]));
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string batchId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM pending_batches WHERE batch_id = $batch";
            cmd.Parameters.AddWithValue("$batch", batchId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> CountAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pending_batches";
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Persists the last applied scan config (JSON) so a restarted
    /// agent resumes its previous config without waiting for a new push.</summary>
    public async Task SaveConfigAsync(string configJson, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO app_config (id, config_json, updated_at)
                VALUES (1, $json, $now)
                ON CONFLICT(id) DO UPDATE SET config_json = $json, updated_at = $now
                """;
            cmd.Parameters.AddWithValue("$json", configJson);
            cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> LoadConfigAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT config_json FROM app_config WHERE id = 1";
            return await cmd.ExecuteScalarAsync(ct) as string;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        _connection.Dispose();
        _gate.Dispose();
    }
}
