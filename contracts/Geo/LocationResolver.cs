using System.Globalization;

namespace Sigmap.Contracts.Geo;

public enum LocationStatus
{
    /// <summary>No GPS evidence within the allowed window. Never fabricate a fix.</summary>
    Unlocated,
    /// <summary>Device has its own GPS; resolved from its own track.</summary>
    Direct,
    /// <summary>Resolved from a GPS-equipped device in the same swarm.</summary>
    Interpolated,
}

/// <summary>Outcome of resolving a detection's location.</summary>
public readonly record struct LocationResult(LocationStatus Status, double? Lat, double? Lon, string Reason)
{
    public static readonly LocationResult UnlocatedDefault = new(LocationStatus.Unlocated, null, null, "no GPS evidence within window");

    public bool HasPosition => Lat is not null && Lon is not null;
}

/// <summary>A single GPS fix, times in unix epoch milliseconds.</summary>
public readonly record struct GpsFix(double Lat, double Lon, double AccuracyM, long AtUnixMs);

/// <summary>GPS track for one device in a swarm.</summary>
public sealed record DeviceGpsTrack(string DeviceId, bool HasGps, IReadOnlyList<GpsFix> Fixes)
{
    public static readonly DeviceGpsTrack None = new(string.Empty, false, Array.Empty<GpsFix>());
}

/// <summary>
/// Resolves a detection's location from GPS evidence available in its swarm.
/// </summary>
/// <remarks>
/// v1 strategy: time-based interpolation. A GPS-equipped device's detection is
/// resolved against its own track; a GPS-less device's detection is resolved
/// against the nearest GPS-equipped device in the same swarm (nearest fix in
/// time, or linear interpolation between the two fixes bracketing the detection
/// timestamp). If no GPS evidence exists within <paramref name="maxGpsWindow"/>
/// the result is <see cref="LocationStatus.Unlocated"/> — never fabricated.
/// </remarks>
public interface ILocationResolver
{
    LocationResult Resolve(
        long detectionAtUnixMs,
        string sourceDeviceId,
        IReadOnlyList<DeviceGpsTrack> swarmTracks,
        TimeSpan maxGpsWindow);
}

/// <summary>Default time-based interpolation resolver.</summary>
public sealed class TimeInterpolationLocationResolver : ILocationResolver
{
    public LocationResult Resolve(
        long detectionAtUnixMs,
        string sourceDeviceId,
        IReadOnlyList<DeviceGpsTrack> swarmTracks,
        TimeSpan maxGpsWindow)
    {
        if (swarmTracks.Count == 0)
            return LocationResult.UnlocatedDefault;

        // 1) Prefer the device's own GPS track.
        var own = swarmTracks.FirstOrDefault(t => t.DeviceId == sourceDeviceId);
        if (own is { HasGps: true, Fixes.Count: > 0 })
        {
            var ownResult = InterpolateTrack(own.Fixes, detectionAtUnixMs, maxGpsWindow);
            if (ownResult.Status != LocationStatus.Unlocated)
                return ownResult;
            // Own GPS exists but no fix inside the window; still consider swarm
            // members below before giving up (a stale own fix may be worse than
            // a fresh neighbour fix).
        }

        // 2) Nearest GPS-equipped swarm member by fix-time distance.
        DeviceGpsTrack? bestTrack = null;
        GpsFix bestBefore = default;
        GpsFix bestAfter = default;
        bool hasBest = false;
        long bestGap = long.MaxValue;

        foreach (var track in swarmTracks)
        {
            if (!track.HasGps || track.Fixes.Count == 0)
                continue;

            var (before, after) = BracketingFixes(track.Fixes, detectionAtUnixMs);
            if (before is null && after is null)
                continue;

            var gapBefore = before is not null ? detectionAtUnixMs - before.Value.AtUnixMs : long.MaxValue;
            var gapAfter = after is not null ? after.Value.AtUnixMs - detectionAtUnixMs : long.MaxValue;
            var nearestGap = Math.Min(gapBefore, gapAfter);
            if (nearestGap < bestGap)
            {
                bestGap = nearestGap;
                bestTrack = track;
                bestBefore = before ?? after!.Value;
                bestAfter = after ?? before!.Value;
                hasBest = true;
            }
        }

        if (!hasBest || bestTrack is null)
            return LocationResult.UnlocatedDefault;

        // Nearest fix must be within the allowed window.
        if (bestGap > maxGpsWindow.TotalMilliseconds)
            return LocationResult.UnlocatedDefault;

        var result = Interpolate(bestBefore, bestAfter, detectionAtUnixMs);
        return new LocationResult(
            LocationStatus.Interpolated,
            result.Lat,
            result.Lon,
            string.Create(CultureInfo.InvariantCulture,
                $"interpolated from GPS device {bestTrack.DeviceId} (gap {bestGap}ms)"));
    }

    private static LocationResult InterpolateTrack(IReadOnlyList<GpsFix> fixes, long t, TimeSpan window)
    {
        var (before, after) = BracketingFixes(fixes, t);
        if (before is null && after is null)
            return LocationResult.UnlocatedDefault;

        GpsFix b = before ?? after!.Value;
        GpsFix a = after ?? before!.Value;

        if (Math.Abs(t - b.AtUnixMs) > window.TotalMilliseconds
            && Math.Abs(t - a.AtUnixMs) > window.TotalMilliseconds)
            return LocationResult.UnlocatedDefault;

        var p = Interpolate(b, a, t);
        return new LocationResult(LocationStatus.Direct, p.Lat, p.Lon, "from device GPS track");
    }

    /// <summary>Linear interpolation between the two fixes bracketing <paramref name="t"/>.</summary>
    private static (double Lat, double Lon) Interpolate(GpsFix before, GpsFix after, long t)
    {
        if (before.AtUnixMs == after.AtUnixMs)
            return (after.Lat, after.Lon);

        if (t <= before.AtUnixMs)
            return (before.Lat, before.Lon);
        if (t >= after.AtUnixMs)
            return (after.Lat, after.Lon);

        var ratio = (double)(t - before.AtUnixMs) / (after.AtUnixMs - before.AtUnixMs);
        return (
            before.Lat + (after.Lat - before.Lat) * ratio,
            before.Lon + (after.Lon - before.Lon) * ratio);
    }

    private static (GpsFix? Before, GpsFix? After) BracketingFixes(IReadOnlyList<GpsFix> fixes, long t)
    {
        GpsFix? before = null;
        GpsFix? after = null;
        foreach (var fix in fixes)
        {
            if (fix.AtUnixMs <= t && (before is null || fix.AtUnixMs > before.Value.AtUnixMs))
                before = fix;
            if (fix.AtUnixMs >= t && (after is null || fix.AtUnixMs < after.Value.AtUnixMs))
                after = fix;
        }
        return (before, after);
    }
}
