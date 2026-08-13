using Microsoft.EntityFrameworkCore;
using Sigmap.Application.Dtos;
using Sigmap.Application.Services;
using Sigmap.Domain.Enums;
using Sigmap.Infrastructure.Persistence;

namespace Sigmap.Infrastructure.Services;

public sealed class FleetService(SigmapDbContext db) : IFleetService
{
    public async Task<List<FleetDeviceDto>> ListAsync(Guid? sessionId, CancellationToken ct)
    {
        var devices = await db.Devices.AsNoTracking().ToListAsync(ct);
        var memberships = await db.SessionDevices.AsNoTracking()
            .Where(sd => sessionId == null || sd.SessionId == sessionId)
            .ToListAsync(ct);
        var sessionIds = memberships.Select(m => m.SessionId).Distinct().ToList();
        var swarmIds = memberships.Where(m => m.SwarmId != null).Select(m => m.SwarmId!.Value).Distinct().ToList();
        var sessions = await db.Sessions.AsNoTracking().Where(s => sessionIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var swarms = await db.Swarms.AsNoTracking().Where(w => swarmIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, ct);
        var presets = await db.Presets.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);

        var result = new List<FleetDeviceDto>(devices.Count);
        foreach (var device in devices)
        {
            var membership = memberships.FirstOrDefault(m => m.DeviceId == device.Id);
            var session = membership is null ? null : sessions.GetValueOrDefault(membership.SessionId);
            var swarm = membership?.SwarmId is null ? null : swarms.GetValueOrDefault(membership.SwarmId.Value);
            var config = membership is null ? null : Mappers.ToDto(membership);
            var preset = config?.PresetId is null ? null : presets.GetValueOrDefault(config.PresetId.Value);

            result.Add(new FleetDeviceDto(
                Mappers.ToDto(device),
                membership?.SessionId,
                session?.Name,
                membership?.SwarmId,
                swarm?.Name,
                membership?.Role,
                Drift: config != null && config.PushState != PushState.Acked,
                config?.PresetId,
                preset?.Name,
                config));
        }

        var rank = new Dictionary<DeviceStatus, int>
        {
            [DeviceStatus.Online] = 0,
            [DeviceStatus.Offline] = 1,
            [DeviceStatus.Error] = 2,
            [DeviceStatus.Pending] = 3,
        };
        return result
            .OrderBy(f => rank[f.Device.Status])
            .ThenBy(f => f.Device.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
