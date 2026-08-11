using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent.Buffer;

/// <summary>Publishes an envelope (a detection batch) and reports success only
/// after the broker has confirmed it.</summary>
public interface IDetectionPublisher
{
    Task PublishAsync(Envelope envelope, CancellationToken ct);
}

/// <summary>
/// Durable offline persistence for batches that could not reach the broker
/// (e.g. no backhaul while driving). Keyed by batch id so replays stay
/// idempotent at the backend.
/// </summary>
public interface IOfflineStore
{
    Task PersistAsync(Envelope envelope, CancellationToken ct);
    Task<IReadOnlyList<Envelope>> PeekOldestAsync(int max, CancellationToken ct);
    Task DeleteAsync(string batchId, CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
}
