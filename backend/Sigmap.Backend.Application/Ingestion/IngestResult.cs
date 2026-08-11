using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Application.Ingestion;

public enum IngestStatus
{
    /// <summary>Batch written (or legitimately skipped, e.g. device not in a session).</summary>
    Handled,
    /// <summary>Batch already processed before — idempotent no-op.</summary>
    Duplicate,
    /// <summary>Malformed / unparseable.</summary>
    Rejected,
}

public sealed record IngestResult(IngestStatus Status, int DetectionCount);
