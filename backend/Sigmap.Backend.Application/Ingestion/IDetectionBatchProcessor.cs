using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Application.Ingestion;

/// <summary>
/// Processes an ingested detection batch: resolves locations against the
/// swarm, writes Postgres in one transaction (idempotent by batch_id), then
/// publishes the located batch for live streaming. Consumers ack the broker
/// message only after this returns with Handled/Duplicate.
/// </summary>
public interface IDetectionBatchProcessor
{
    Task<IngestResult> ProcessAsync(DetectionBatch batch, CancellationToken ct);
}
