using System.Threading.Channels;
using Grpc.AspNetCore.Web;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Sigmap.Application.Abstractions;
using Sigmap.Application.Json;
using Sigmap.Domain.Enums;
using Sigmap.Infrastructure.Persistence;
using LiveProto = Sigmap.Api.Live;
using Ev = Sigmap.Application.Abstractions.LiveEvent;
using HBeat = Sigmap.Application.Abstractions.Heartbeat;
using LD = Sigmap.Application.Abstractions.LocatedDetection;

namespace Sigmap.Api.Live;

/// <summary>gRPC-Web server-streaming live feed (maps to the frontend LiveEvent oneof).</summary>
[Authorize]
[EnableGrpcWeb]
public sealed class LiveService(ILiveEventBus bus, IServiceScopeFactory scopeFactory)
    : LiveProto.LiveStream.LiveStreamBase
{
    public override async Task Subscribe(
        LiveProto.SubscribeRequest request,
        IServerStreamWriter<LiveProto.LiveEvent> responseStream,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.SessionId, out var sessionId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid session_id"));
        }

        var fleet = await BuildFleetSnapshotAsync(sessionId, context.CancellationToken);
        if (fleet is not null)
        {
            await responseStream.WriteAsync(ToProto(fleet));
        }

        var outbound = Channel.CreateUnbounded<Ev>();
        using var _ = bus.Subscribe(sessionId, ev => outbound.Writer.TryWrite(ev));

        try
        {
            await foreach (var ev in outbound.Reader.ReadAllAsync(context.CancellationToken))
            {
                await responseStream.WriteAsync(ToProto(ev));
            }
        }
        catch (OperationCanceledException)
        {
            // client disconnected
        }
    }

    private async Task<Ev.Fleet?> BuildFleetSnapshotAsync(Guid sessionId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SigmapDbContext>();

        var members = await db.SessionDevices.AsNoTracking()
            .Where(sd => sd.SessionId == sessionId)
            .Select(sd => sd.DeviceId)
            .ToListAsync(ct);
        if (members.Count == 0) return null;

        var devices = await db.Devices.AsNoTracking().Where(d => members.Contains(d.Id)).ToListAsync(ct);
        var heartbeats = devices.Select(ToHeartbeat).ToList();
        return new Ev.Fleet { SessionId = sessionId, Devices = heartbeats };
    }

    private static HBeat ToHeartbeat(Sigmap.Domain.Entities.Device device)
    {
        var caps = Sigmap.Infrastructure.Services.Mappers.Capabilities(device.CapabilitiesJson);
        return new HBeat
        {
            DeviceId = device.Id,
            At = device.LastHeartbeatAt ?? DateTimeOffset.UtcNow,
            Status = device.Status == DeviceStatus.Error ? "error" : "online",
            CurrentChannel = device.Status == DeviceStatus.Online ? 6 : null,
            BatteryPct = caps.HasBattery ? 78 : null,
            GpsFix = caps.HasGps,
            DetectionsBuffered = 0,
            DetectionsSentTotal = 0,
        };
    }

    private static LiveProto.LiveEvent ToProto(Ev ev) => ev switch
    {
        Ev.Detections d => new LiveProto.LiveEvent
        {
            SessionId = d.SessionId.ToString(),
            Detections = new LiveProto.DetectionBatch
            {
                BatchId = d.BatchId,
                DeviceId = d.DeviceId.ToString(),
                Detections = { d.Items.Select(x => ToProto(x)) },
            },
        },
        Ev.Device dev => new LiveProto.LiveEvent
        {
            SessionId = dev.SessionId.ToString(),
            Device = ToProto(dev.Heartbeat),
        },
        Ev.Config c => new LiveProto.LiveEvent
        {
            SessionId = c.SessionId.ToString(),
            Config = new LiveProto.ConfigPush
            {
                DeviceId = c.DeviceId.ToString(),
                PushId = c.PushId,
                Status = EnumStrings.ToString(c.Status),
            },
        },
        Ev.Fleet f => new LiveProto.LiveEvent
        {
            SessionId = f.SessionId.ToString(),
            Fleet = new LiveProto.FleetSnapshot
            {
                Devices = { f.Devices.Select(x => ToProto(x)) },
            },
        },
        _ => throw new ArgumentOutOfRangeException(nameof(ev)),
    };

    private static LiveProto.LocatedDetection ToProto(LD d)
    {
        var p = new LiveProto.LocatedDetection
        {
            Mac = d.Mac,
            DeviceType = EnumStrings.ToString(d.DeviceType),
            SignalDbm = d.SignalDbm,
            Channel = d.Channel,
            Encryption = EnumStrings.ToString(d.Encryption),
            DetectedAt = d.DetectedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            LocationFlag = EnumStrings.ToString(d.LocationFlag),
        };
        if (d.Ssid is not null) p.Ssid = d.Ssid;
        if (d.BtName is not null) p.BtName = d.BtName;
        if (d.Lat is not null) p.Lat = d.Lat.Value;
        if (d.Lon is not null) p.Lon = d.Lon.Value;
        if (d.VendorName is not null) p.VendorName = d.VendorName;
        return p;
    }

    private static LiveProto.Heartbeat ToProto(HBeat h)
    {
        var p = new LiveProto.Heartbeat
        {
            DeviceId = h.DeviceId.ToString(),
            At = h.At.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            Status = h.Status,
            GpsFix = h.GpsFix,
            DetectionsBuffered = h.DetectionsBuffered,
            DetectionsSentTotal = h.DetectionsSentTotal,
        };
        if (h.CurrentChannel is not null) p.CurrentChannel = h.CurrentChannel.Value;
        if (h.BatteryPct is not null) p.BatteryPct = h.BatteryPct.Value;
        return p;
    }
}
