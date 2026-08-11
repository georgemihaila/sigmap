using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Sigmap.Backend.Infrastructure.Persistence;

namespace Sigmap.Backend.Api.Endpoints;

public static class CoverageEndpoints
{
    public static RouteGroupBuilder MapCoverageEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/sessions/{sessionId:guid}/coverage", GetCoverage);
        api.MapPost("/sessions/{sessionId:guid}/gpx", ImportGpx);
        api.MapGet("/sessions/compare", Compare);
        return api;
    }

    /// <summary>Heatmap/coverage points actually scanned by devices in a session.</summary>
    private static async Task<Results<Ok<CoverageResult>, NotFound>> GetCoverage(
        Guid sessionId, SigmapDbContext db, int limit = 5000, CancellationToken ct = default)
    {
        var exists = await db.Sessions.AnyAsync(s => s.Id == sessionId, ct);
        if (!exists)
            return TypedResults.NotFound();

        limit = Math.Clamp(limit, 1, 50_000);
        // Thinned sample so the browser stays responsive on long sessions.
        var total = await db.SessionGpsSamples.CountAsync(s => s.SessionId == sessionId, ct);
        var skip = total > limit ? total / limit : 0;

        var points = await db.SessionGpsSamples.AsNoTracking()
            .Where(s => s.SessionId == sessionId)
            .OrderBy(s => s.At)
            .Skip(skip)
            .Select(s => new CoveragePoint(s.Geom.Y, s.Geom.X, s.At))
            .ToListAsync(ct);

        return TypedResults.Ok(new CoverageResult(sessionId, points, points.Count));
    }

    /// <summary>Parses a planned GPX route and persists it as coverage points so
    /// the UI can show planned vs actual.</summary>
    private static async Task<Results<Ok<int>, BadRequest<string>>> ImportGpx(
        Guid sessionId, HttpRequest request, SigmapDbContext db, CancellationToken ct)
    {
        string xml;
        using (var reader = new StreamReader(request.Body))
        {
            xml = await reader.ReadToEndAsync(ct);
        }

        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            var ns = System.Xml.Linq.XNamespace.Get("http://www.topografix.com/GPX/1/1");
            var points = new List<Domain.Entities.SessionGpsSample>();
            var seq = 0;
            foreach (var trkpt in doc.Descendants(ns + "trkpt"))
            {
                if (double.TryParse(trkpt.Attribute("lat")?.Value, out var lat)
                    && double.TryParse(trkpt.Attribute("lon")?.Value, out var lon))
                {
                    points.Add(new Domain.Entities.SessionGpsSample
                    {
                        SessionId = sessionId,
                        DeviceId = Guid.Empty,
                        Geom = new NetTopologySuite.Geometries.Point(lon, lat) { SRID = 4326 },
                        At = DateTimeOffset.UtcNow.AddSeconds(seq++),
                    });
                }
            }

            if (points.Count == 0)
                return TypedResults.BadRequest("No track points found in GPX");

            await db.SessionGpsSamples.AddRangeAsync(points, ct);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(points.Count);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Invalid GPX: {ex.Message}");
        }
    }

    /// <summary>Set-level comparison of two sessions' detected devices.</summary>
    private static async Task<Results<Ok<ComparisonResult>, NotFound>> Compare(
        Guid a, Guid b, SigmapDbContext db, CancellationToken ct)
    {
        var sessions = await db.Sessions.CountAsync(s => s.Id == a || s.Id == b, ct);
        if (sessions != 2)
            return TypedResults.NotFound();

        var macsA = await db.Detections.Where(d => d.SessionId == a)
            .Select(d => d.Mac).Distinct().ToListAsync(ct);
        var macsB = await db.Detections.Where(d => d.SessionId == b)
            .Select(d => d.Mac).Distinct().ToListAsync(ct);

        var setA = macsA.Select(x => x.ToLowerInvariant()).ToHashSet();
        var setB = macsB.Select(x => x.ToLowerInvariant()).ToHashSet();

        return TypedResults.Ok(new ComparisonResult(
            SessionA: a,
            SessionB: b,
            OnlyInA: setA.Except(setB).Count(),
            OnlyInB: setB.Except(setA).Count(),
            InBoth: setA.Intersect(setB).Count(),
            TotalA: setA.Count,
            TotalB: setB.Count));
    }

    public sealed record CoveragePoint(double Lat, double Lon, DateTimeOffset At);
    public sealed record CoverageResult(Guid SessionId, List<CoveragePoint> Points, int Count);
    public sealed record ComparisonResult(Guid SessionA, Guid SessionB, int OnlyInA, int OnlyInB, int InBoth, int TotalA, int TotalB);
}
