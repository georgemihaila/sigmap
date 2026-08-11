using Google.Protobuf;
using Sigmap.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Sigmap.Backend.Application.Configuration;
using Sigmap.Backend.Infrastructure.Persistence;
using Sigmap.Contracts.Messages;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Infrastructure.Messaging;

/// <summary>Publishes scan-config pushes to a device's durable per-device queue.</summary>
public sealed class RabbitMqConfigPushPublisher : IConfigPushPublisher
{
    private readonly RabbitMqConnectionProvider _connection;
    private readonly SigmapDbContext _db;

    public RabbitMqConfigPushPublisher(RabbitMqConnectionProvider connection, SigmapDbContext db)
    {
        _connection = connection;
        _db = db;
    }

    public async Task<ConfigPush> PushAsync(Guid sessionId, Guid deviceId, ScanConfig config, Guid? presetId, CancellationToken ct)
    {
        var push = new ConfigPush
        {
            PushId = Guid.NewGuid().ToString("N"),
            DeviceId = deviceId.ToString(),
            SessionId = sessionId.ToString(),
            PresetId = presetId?.ToString() ?? string.Empty,
            Config = config,
            IssuedAtUnixMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        var envelope = EnvelopeFactory.For(push.PushId, push);
        await _connection.PublishAsync(
            RabbitMqTopology.ConfigExchange,
            RabbitMqTopology.ConfigKey(deviceId.ToString()),
            envelope.ToByteArray(),
            persistent: true,
            ct);

        return push;
    }

    /// <summary>Ensures the device's durable config queue exists (pairing + offline-first).</summary>
    public static async Task EnsureDeviceQueueAsync(
        RabbitMqConnectionProvider connection, string deviceId, CancellationToken ct)
    {
        var conn = await connection.GetConnectionAsync(ct);
        await using var channel = await conn.CreateChannelAsync(null, ct);
        await RabbitMqTopology.DeclareAsync(channel, ct);
        await channel.QueueDeclareAsync(RabbitMqTopology.ConfigQueue(deviceId), true, false, false, null, false, false, ct);
        await channel.QueueBindAsync(
            RabbitMqTopology.ConfigQueue(deviceId),
            RabbitMqTopology.ConfigExchange,
            RabbitMqTopology.ConfigKey(deviceId),
            null,
            false,
            ct);
    }
}
