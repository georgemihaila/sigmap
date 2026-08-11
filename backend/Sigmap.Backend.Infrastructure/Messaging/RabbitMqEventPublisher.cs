using Google.Protobuf;
using Sigmap.Contracts.Messaging;
using Microsoft.Extensions.Logging;
using Sigmap.Backend.Application.Messaging;
using Sigmap.Contracts.Messages;
using Sigmap.Contracts.Proto;
using Sigmap.Contracts.Proto.Live;

namespace Sigmap.Backend.Infrastructure.Messaging;

/// <summary>Publishes internal events to <c>sigmap.events</c> for the BFF to
/// re-broadcast to browsers over gRPC-Web.</summary>
public sealed class RabbitMqEventPublisher : IEventPublisher
{
    private readonly RabbitMqConnectionProvider _connection;
    private readonly ILogger<RabbitMqEventPublisher> _log;

    public RabbitMqEventPublisher(RabbitMqConnectionProvider connection, ILogger<RabbitMqEventPublisher> log)
    {
        _connection = connection;
        _log = log;
    }

    public Task PublishLocatedBatchAsync(LocatedBatch located, CancellationToken ct)
    {
        var evt = new LiveEvent { Detections = located };
        return PublishAsync(evt, $"event.detections.{located.DeviceId}", ct);
    }

    public Task PublishDeviceStatusAsync(DeviceStatusUpdate status, CancellationToken ct) =>
        PublishAsync(new LiveEvent { Device = status }, $"event.device.{status.DeviceId}", ct);

    public Task PublishConfigPushStatusAsync(ConfigPushStatus status, CancellationToken ct) =>
        PublishAsync(new LiveEvent { Config = status }, $"event.config.{status.DeviceId}", ct);

    public Task PublishFleetSnapshotAsync(FleetSnapshot fleet, CancellationToken ct) =>
        PublishAsync(new LiveEvent { Fleet = fleet }, "event.fleet", ct);

    private async Task PublishAsync(LiveEvent evt, string routingKey, CancellationToken ct)
    {
        var envelope = EnvelopeFactory.For($"evt-{Guid.NewGuid():N}", evt);
        await _connection.PublishAsync(
            RabbitMqTopology.EventsExchange,
            routingKey,
            envelope.ToByteArray(),
            persistent: false,
            ct);
    }
}
