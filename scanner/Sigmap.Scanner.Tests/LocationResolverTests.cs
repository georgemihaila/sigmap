using Sigmap.Contracts.Geo;

namespace Sigmap.Scanner.Tests;

public class LocationResolverTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly TimeInterpolationLocationResolver _resolver = new();

    private static DeviceGpsTrack Track(string id, params (long T, double Lat, double Lon)[] fixes) =>
        new(id, true, fixes.Select(f => new GpsFix(f.Lat, f.Lon, 5, f.T)).ToList());

    [Fact]
    public void Own_gps_track_resolves_direct()
    {
        var t = 100_000;
        var result = _resolver.Resolve(t, "A", new[] { Track("A", (t - 1000, 52.0, 13.0), (t + 1000, 52.02, 13.02)) }, Window);

        Assert.Equal(LocationStatus.Direct, result.Status);
        Assert.True(result.HasPosition);
        Assert.Equal(52.01, result.Lat!.Value, 2);
        Assert.Equal(13.01, result.Lon!.Value, 2);
    }

    [Fact]
    public void Own_track_with_no_fix_in_window_uses_swarm_member()
    {
        var t = 100_000;
        var tracks = new[]
        {
            Track("A", (t - 3_600_000, 51.0, 10.0), (t + 3_600_000, 51.0, 10.0)), // stale own
            Track("B", (t - 1000, 52.0, 13.0), (t + 1000, 52.02, 13.02)),         // fresh swarm
        };
        var result = _resolver.Resolve(t, "A", tracks, Window);

        Assert.Equal(LocationStatus.Interpolated, result.Status);
        Assert.Equal(52.01, result.Lat!.Value, 2);
        Assert.Contains("B", result.Reason);
    }

    [Fact]
    public void No_gps_evidence_returns_unlocated()
    {
        var t = 100_000;
        var result = _resolver.Resolve(t, "A", Array.Empty<DeviceGpsTrack>(), Window);

        Assert.Equal(LocationStatus.Unlocated, result.Status);
        Assert.False(result.HasPosition);
    }

    [Fact]
    public void Swarm_fix_outside_window_returns_unlocated()
    {
        var t = 100_000;
        var result = _resolver.Resolve(t, "A", new[] { Track("B", (t - 10_000_000, 52.0, 13.0)) }, Window);

        Assert.Equal(LocationStatus.Unlocated, result.Status);
    }

    [Fact]
    public void Clamps_to_single_fix_when_only_one_side_exists()
    {
        var t = 100_000;
        var result = _resolver.Resolve(t, "A", new[] { Track("B", (t + 2000, 52.5, 13.5)) }, Window);

        Assert.Equal(LocationStatus.Interpolated, result.Status);
        Assert.Equal(52.5, result.Lat!.Value, 2);
    }
}
