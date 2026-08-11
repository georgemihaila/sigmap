using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sigmap.Backend.Application.Messaging;
using Sigmap.Backend.Infrastructure.Persistence;
using Sigmap.Contracts.Proto;
using Sigmap.Contracts.Proto.Live;

namespace Sigmap.Backend.Infrastructure.Messaging;

/// <summary>Applies heartbeat state to a device and forwards to the live stream.</summary>
public sealed class HeartbeatProcessor
{
    private readonly SigmapDbContext _db;
    private readonly IEventPublisher _events;
    private readonly ILogger<HeartbeatProcessor> _log;

    public HeartbeatProcessor(SigmapDbContext db, IEventPublisher events, ILogger<HeartbeatProcessor> log)
    {
        _db = db;
        _events = events;
        _log = log;
    }

    public async Task ProcessAsync(Heartbeat heartbeat, CancellationToken ct)
    {
        if (!Guid.TryParse(heartbeat.DeviceId, out var deviceId))
        {
            _log.LogWarning("Heartbeat from unparseable device id '{DeviceId}'", heartbeat.DeviceId);
            return;
        }

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (device is null)
            return; // not a paired device; ignore

        device.Status = heartbeat.Status switch
        {
            DeviceStatus.Online or DeviceStatus.Busy => Domain.DeviceStatus.Online,
            DeviceStatus.Error => Domain.DeviceStatus.Error,
            _ => Domain.DeviceStatus.Offline,
        };
        device.LastHeartbeatAt = DateTimeOffset.FromUnixTimeMilliseconds((long)heartbeat.AtUnixMs);
        await _db.SaveChangesAsync(ct);

        await _events.PublishDeviceStatusAsync(
            new DeviceStatusUpdate { DeviceId = heartbeat.DeviceId, Heartbeat = heartbeat }, ct);
    }
}
