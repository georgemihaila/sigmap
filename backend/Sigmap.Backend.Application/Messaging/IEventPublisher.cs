using Sigmap.Contracts.Proto;
using Sigmap.Contracts.Proto.Live;

namespace Sigmap.Backend.Application.Messaging;

/// <summary>Publishes internal events to the events exchange consumed by the BFF.</summary>
public interface IEventPublisher
{
    Task PublishLocatedBatchAsync(LocatedBatch located, CancellationToken ct);
    Task PublishDeviceStatusAsync(DeviceStatusUpdate status, CancellationToken ct);
    Task PublishConfigPushStatusAsync(ConfigPushStatus status, CancellationToken ct);
    Task PublishFleetSnapshotAsync(FleetSnapshot fleet, CancellationToken ct);
}
