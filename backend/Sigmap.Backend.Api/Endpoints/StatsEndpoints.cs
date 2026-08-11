using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Sigmap.Backend.Infrastructure.Persistence;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Api.Endpoints;

public static class StatsEndpoints
{
    public static RouteGroupBuilder MapStatsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/sessions/{sessionId:guid}/stats", SessionStats);
        return api;
    }

    private static async Task<Results<Ok<SessionStatsDto>, NotFound>> SessionStats(
        Guid sessionId, SigmapDbContext db, CancellationToken ct)
    {
        var exists = await db.Sessions.AnyAsync(s => s.Id == sessionId, ct);
        if (!exists)
            return TypedResults.NotFound();

        var encryptions = await db.Detections.AsNoTracking()
            .Where(d => d.SessionId == sessionId && d.DeviceType == DeviceType.Ap && d.Encryption != Encryption.Unspecified)
            .GroupBy(d => d.Encryption)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var vendors = await db.DetectedDevices.AsNoTracking()
            .Where(dd => dd.VendorName != null && db.Detections.Any(d => d.SessionId == sessionId && d.Mac == dd.Mac))
            .GroupBy(dd => dd.VendorName!)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(20)
            .ToListAsync(ct);

        var counts = await db.Detections.AsNoTracking()
            .Where(d => d.SessionId == sessionId)
            .GroupBy(d => d.DeviceType)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var located = await db.Detections.AsNoTracking()
            .CountAsync(d => d.SessionId == sessionId && d.LocationFlag != Domain.LocationFlag.Unlocated, ct);
        var total = await db.Detections.AsNoTracking().CountAsync(d => d.SessionId == sessionId, ct);

        return TypedResults.Ok(new SessionStatsDto(
            EncryptionBreakdown: encryptions.ToDictionary(e => e.Key.ToString(), e => e.Count),
            VendorBreakdown: vendors.ToDictionary(v => v.Key, v => v.Count),
            DeviceTypeBreakdown: counts.ToDictionary(c => c.Key.ToString(), c => c.Count),
            TotalDetections: total,
            LocatedDetections: located));
    }

    public sealed record SessionStatsDto(
        Dictionary<string, int> EncryptionBreakdown,
        Dictionary<string, int> VendorBreakdown,
        Dictionary<string, int> DeviceTypeBreakdown,
        int TotalDetections,
        int LocatedDetections);
}
