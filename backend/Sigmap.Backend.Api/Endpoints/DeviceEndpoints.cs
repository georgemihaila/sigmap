using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Sigmap.Backend.Domain.Entities;
using Sigmap.Backend.Infrastructure.Persistence;

namespace Sigmap.Backend.Api.Endpoints;

public static class DeviceEndpoints
{
    public static RouteGroupBuilder MapDeviceEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/devices");

        g.MapGet("/", ListDevices);
        g.MapGet("/{id:guid}", GetDevice);
        g.MapGet("/detected", ListDetectedDevices);
        g.MapGet("/detected/{mac}", GetDetectedDevice);
        g.MapGet("/detected/{mac}/signal-series", GetSignalSeries);

        return g;
    }

    private static async Task<Ok<List<Device>>> ListDevices(SigmapDbContext db, CancellationToken ct)
    {
        var devices = await db.Devices.OrderByDescending(d => d.PairedAt).ToListAsync(ct);
        return TypedResults.Ok(devices);
    }

    private static async Task<Results<Ok<Device>, NotFound>> GetDevice(Guid id, SigmapDbContext db, CancellationToken ct)
    {
        var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == id, ct);
        return device is null ? TypedResults.NotFound() : TypedResults.Ok(device);
    }

    private static async Task<Ok<DetectedDevicePage>> ListDetectedDevices(
        SigmapDbContext db,
        string? cursor = null,
        int limit = 100,
        Guid? session = null,
        string? bbox = null,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var query = db.DetectedDevices.AsNoTracking();

        if (session is not null)
        {
            query = query.Where(dd => db.Detections
                .Where(d => d.SessionId == session && d.Mac == dd.Mac)
                .Any());
        }

        var decoded = KeysetCursor.Decode(cursor);
        if (decoded is not null)
        {
            var sort = new DateTimeOffset(decoded.K, TimeSpan.Zero);
            query = query.Where(dd => dd.LastSeenAt < sort
                                      || (dd.LastSeenAt == sort
                                          && string.Compare(dd.MacNormalized, decoded.I, StringComparison.Ordinal) < 0));
        }

        var page = await query
            .OrderByDescending(dd => dd.LastSeenAt)
            .ThenByDescending(dd => dd.MacNormalized)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = page.Count > limit;
        var items = page.Take(limit).ToList();
        var next = hasMore && items.Count > 0
            ? KeysetCursor.Encode(items[^1].LastSeenAt, items[^1].MacNormalized)
            : null;

        return TypedResults.Ok(new DetectedDevicePage(items, next, items.Count));
    }

    private static async Task<Results<Ok<DetectedDeviceDetail>, NotFound>> GetDetectedDevice(
        string mac, SigmapDbContext db, CancellationToken ct)
    {
        var normalized = Sigmap.Backend.Application.Lookup.OuiLookup.NormalizeMac(mac);
        var device = await db.DetectedDevices.AsNoTracking()
            .FirstOrDefaultAsync(dd => dd.MacNormalized == normalized, ct);
        if (device is null)
            return TypedResults.NotFound();

        var sessionIds = await db.Detections
            .Where(d => d.Mac == device.Mac || d.Mac == device.Mac.ToUpperInvariant() || d.Mac == device.Mac.ToLowerInvariant())
            .Select(d => d.SessionId)
            .Distinct()
            .ToListAsync(ct);
        var sessions = await db.Sessions
            .Where(s => sessionIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.Status })
            .ToListAsync(ct);

        return TypedResults.Ok(new DetectedDeviceDetail(device, sessions.Select(s => new SessionRef(s.Id, s.Name, s.Status.ToString()))));
    }

    private static async Task<Results<Ok<List<SignalPoint>>, NotFound>> GetSignalSeries(
        string mac, SigmapDbContext db, CancellationToken ct)
    {
        var normalized = Sigmap.Backend.Application.Lookup.OuiLookup.NormalizeMac(mac);
        var device = await db.DetectedDevices.AsNoTracking()
            .FirstOrDefaultAsync(dd => dd.MacNormalized == normalized, ct);
        if (device is null)
            return TypedResults.NotFound();

        var points = await db.Detections.AsNoTracking()
            .Where(d => d.Mac == device.Mac)
            .OrderBy(d => d.DetectedAt)
            .Select(d => new SignalPoint(
                d.DetectedAt,
                d.SignalDbm,
                d.Channel,
                d.LocationFlag.ToString(),
                d.Geom != null ? d.Geom.X : null,
                d.Geom != null ? d.Geom.Y : null))
            .ToListAsync(ct);

        return TypedResults.Ok(points);
    }

    public sealed record DetectedDevicePage(List<DetectedDevice> Items, string? NextCursor, int Count);
    public sealed record SessionRef(Guid Id, string Name, string Status);
    public sealed record DetectedDeviceDetail(DetectedDevice Device, IEnumerable<SessionRef> Sessions);
    public sealed record SignalPoint(DateTimeOffset At, int SignalDbm, int Channel, string LocationFlag, double? Lon, double? Lat);
}
