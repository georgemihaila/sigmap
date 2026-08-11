namespace Sigmap.Backend.Domain.Entities;

/// <summary>Idempotency gate: unique (device_id, batch_id).</summary>
public class DetectionBatch
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string BatchId { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Detection> Detections { get; set; } = new List<Detection>();
}
