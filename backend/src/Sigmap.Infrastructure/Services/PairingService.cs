using Microsoft.EntityFrameworkCore;
using Sigmap.Application.Abstractions;
using Sigmap.Application.Dtos;
using Sigmap.Application.Services;
using Sigmap.Domain.Entities;
using Sigmap.Domain.Enums;
using Sigmap.Infrastructure.Persistence;

namespace Sigmap.Infrastructure.Services;

public sealed class PairingService(SigmapDbContext db) : IPairingService
{
    public Task<List<PairingRequestDto>> ListPendingAsync(CancellationToken ct) =>
        db.Pairings.AsNoTracking()
            .Where(p => p.Status == PairingStatus.Pending)
            .OrderBy(p => p.RequestedAt)
            .Select(p => Mappers.ToDto(p))
            .ToListAsync(ct);

    public async Task<bool> ApproveAsync(Guid deviceId, Guid? sessionId, CancellationToken ct)
    {
        var pairing = await db.Pairings.FirstOrDefaultAsync(p => p.DeviceId == deviceId, ct);
        if (pairing is null) return false;

        pairing.Status = PairingStatus.Approved;
        pairing.SessionId = sessionId;

        var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (device is null)
        {
            device = new Device
            {
                Id = deviceId,
                Name = pairing.DeviceName,
                Platform = pairing.Platform,
                CapabilitiesJson = pairing.CapabilitiesJson,
                Status = DeviceStatus.Offline,
                LastHeartbeatAt = null,
                LastKnownIp = null,
                PairedAt = DateTimeOffset.UtcNow,
            };
            db.Devices.Add(device);
        }
        else
        {
            device.Status = DeviceStatus.Offline;
            device.PairedAt = DateTimeOffset.UtcNow;
        }

        if (sessionId is not null)
        {
            var exists = await db.SessionDevices.AnyAsync(sd => sd.SessionId == sessionId && sd.DeviceId == deviceId, ct);
            if (!exists)
            {
                var swarmId = await db.Swarms.Where(w => w.SessionId == sessionId).Select(w => (Guid?)w.Id).FirstOrDefaultAsync(ct);
                db.SessionDevices.Add(new SessionDevice
                {
                    SessionId = sessionId.Value,
                    DeviceId = deviceId,
                    SwarmId = swarmId,
                    Role = "scout",
                    JoinedAt = DateTimeOffset.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RejectAsync(Guid deviceId, CancellationToken ct)
    {
        var pairing = await db.Pairings.FirstOrDefaultAsync(p => p.DeviceId == deviceId, ct);
        if (pairing is null) return false;
        pairing.Status = PairingStatus.Rejected;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<PairingQrDto> GetQrAsync(CancellationToken ct)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        return Task.FromResult(new PairingQrDto(token, $"sigmap-pair://?host={Uri.EscapeDataString("__BACKEND_HOST__")}&token={token}"));
    }
}