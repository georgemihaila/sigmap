using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Application.Configuration;

/// <summary>Pushes scan config to a device via the per-device RabbitMQ queue.</summary>
public interface IConfigPushPublisher
{
    Task<ConfigPush> PushAsync(Guid sessionId, Guid deviceId, ScanConfig config, Guid? presetId, CancellationToken ct);
}

/// <summary>Handles ConfigAck messages from devices.</summary>
public interface IConfigAckHandler
{
    Task HandleAsync(ConfigAck ack, CancellationToken ct);
}
