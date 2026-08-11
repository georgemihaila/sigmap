using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sigmap.Backend.Application.Configuration;
using Sigmap.Backend.Application.Pairing;
using Sigmap.Backend.Domain;
using Sigmap.Backend.Domain.Entities;
using Sigmap.Backend.Infrastructure.Messaging;
using Sigmap.Backend.Infrastructure.Persistence;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Infrastructure.Pairing;

public sealed class PairingService : IPairingService
{
    private readonly SigmapDbContext _db;
    private readonly ISessionConfigService _configService;
    private readonly RabbitMqConnectionProvider _connection;
    private readonly string _pairingToken;
    private readonly string _publicHost;
    private readonly ILogger<PairingService> _log;

    public PairingService(
        SigmapDbContext db,
        ISessionConfigService configService,
        RabbitMqConnectionProvider connection,
        IConfiguration configuration,
        ILogger<PairingService> log)
    {
        _db = db;
        _configService = configService;
        _connection = connection;
        _pairingToken = configuration["Pairing:Token"] ?? "sigmap-dev-token";
        _publicHost = configuration["Pairing:PublicHost"] ?? "http://localhost:5080";
        _log = log;
    }

    public async Task<PairingResponse> RequestAsync(PairingRequest request, string? clientIp, CancellationToken ct)
    {
        var expectedHash = Sha256Hex(_pairingToken);
        if (!string.Equals(request.TokenHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            _log.LogWarning("Rejected pairing request from {Ip} with bad token hash", clientIp);
            return new PairingResponse
            {
                Status = PairingStatus.PairingRejected,
                DeviceId = request.DeviceId,
                ServerTimeUnixMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }

        if (!Guid.TryParse(request.DeviceId, out var deviceId))
            deviceId = Guid.NewGuid();

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (device is null)
        {
            device = new Device
            {
                Id = deviceId,
                Name = string.IsNullOrWhiteSpace(request.DeviceName) ? request.DeviceId[..8] : request.DeviceName,
                Platform = request.Capabilities?.Platform ?? "unknown",
                CapabilitiesJson = JsonSerializer.Serialize(request.Capabilities),
                Status = Sigmap.Backend.Domain.DeviceStatus.Pending,
                PairedAt = DateTimeOffset.UtcNow,
                LastKnownIp = clientIp,
            };
            _db.Devices.Add(device);
        }
        else
        {
            device.Status = Sigmap.Backend.Domain.DeviceStatus.Pending;
            device.LastKnownIp = clientIp;
            device.Platform = request.Capabilities?.Platform ?? device.Platform;
        }

        await _db.SaveChangesAsync(ct);

        return new PairingResponse
        {
            Status = PairingStatus.PairingPending,
            DeviceId = request.DeviceId,
            InitialConfig = ScanConfigFactory.Off(),
            ServerTimeUnixMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }

    public async Task<IReadOnlyList<Device>> PendingAsync(CancellationToken ct)
    {
        return await _db.Devices.AsNoTracking()
            .Where(d => d.Status == Sigmap.Backend.Domain.DeviceStatus.Pending)
            .OrderBy(d => d.PairedAt)
            .ToListAsync(ct);
    }

    public async Task<Device> ApproveAsync(Guid deviceId, Guid sessionId, CancellationToken ct)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct)
            ?? throw new InvalidOperationException("Unknown device");

        var session = await _db.Sessions.AnyAsync(s => s.Id == sessionId, ct);
        if (!session)
            throw new InvalidOperationException("Unknown session");

        device.Status = Sigmap.Backend.Domain.DeviceStatus.Offline;
        await _db.SaveChangesAsync(ct);

        var membership = await _db.SessionDevices
            .FirstOrDefaultAsync(sd => sd.SessionId == sessionId && sd.DeviceId == deviceId, ct);
        if (membership is null)
        {
            _db.SessionDevices.Add(new SessionDevice
            {
                SessionId = sessionId,
                DeviceId = deviceId,
                JoinedAt = DateTimeOffset.UtcNow,
            });
        }

        // Offline-first: the device's durable queue exists before the first push.
        await RabbitMqConfigPushPublisher.EnsureDeviceQueueAsync(_connection, deviceId.ToString(), ct);

        await _configService.ApplyAsync(sessionId, deviceId, ScanConfigFactory.Off(), presetId: null, ct);

        _log.LogInformation("Approved device {DeviceId} into session {SessionId}", deviceId, sessionId);
        return device;
    }

    public async Task RejectAsync(Guid deviceId, CancellationToken ct)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (device is not null)
        {
            _db.Devices.Remove(device);
            await _db.SaveChangesAsync(ct);
        }
    }

    public string BuildQrPayload(Guid sessionId)
    {
        var payload = new
        {
            v = 1,
            host = _publicHost,
            token = _pairingToken,
            session = sessionId.ToString(),
        };
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
    }

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
