using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sigmap.Backend.Application.Messaging;
using Sigmap.Backend.Domain;
using Sigmap.Backend.Infrastructure.Persistence;
using Sigmap.Contracts.Proto;
using Sigmap.Contracts.Proto.Live;

namespace Sigmap.Backend.Infrastructure.Messaging;

/// <summary>Applies a device's ConfigAck to the tracked push state and forwards
/// the outcome to the live stream.</summary>
public sealed class ConfigAckProcessor
{
    private readonly SigmapDbContext _db;
    private readonly IEventPublisher _events;
    private readonly ILogger<ConfigAckProcessor> _log;

    public ConfigAckProcessor(SigmapDbContext db, IEventPublisher events, ILogger<ConfigAckProcessor> log)
    {
        _db = db;
        _events = events;
        _log = log;
    }

    public async Task ProcessAsync(ConfigAck ack, CancellationToken ct)
    {
        if (!Guid.TryParse(ack.DeviceId, out var deviceId)
            || !Guid.TryParse(ack.PushId, out var pushId))
        {
            _log.LogWarning("ConfigAck with unparseable ids (device={DeviceId}, push={PushId})", ack.DeviceId, ack.PushId);
            return;
        }

        var row = await _db.SessionDeviceConfigs
            .FirstOrDefaultAsync(c => c.DeviceId == deviceId && c.LastPushId == pushId, ct);
        if (row is null)
        {
            _log.LogInformation("ConfigAck for unknown push {PushId} (device {DeviceId}); ignoring", ack.PushId, ack.DeviceId);
            return;
        }

        row.PushState = ack.Status switch
        {
            ConfigAckStatus.Applied => PushState.Acked,
            ConfigAckStatus.Failed => PushState.Failed,
            _ => PushState.Pending,
        };
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _events.PublishConfigPushStatusAsync(new ConfigPushStatus
        {
            DeviceId = ack.DeviceId,
            PushId = ack.PushId,
            SessionId = row.SessionId.ToString(),
            Status = ack.Status,
        }, ct);
    }
}
