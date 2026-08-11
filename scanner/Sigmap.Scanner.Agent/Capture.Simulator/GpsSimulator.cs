using System.Globalization;
using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent.Capture.Simulator;

/// <summary>
/// Synthetic GPS track along a small route, for local development without a
/// real receiver. Emits a fix roughly once per second.
/// </summary>
public sealed class GpsSimulator
{
    private readonly double _startLat;
    private readonly double _startLon;
    private readonly Random _rng;

    public GpsSimulator(double startLat, double startLon, int seed)
    {
        _startLat = startLat;
        _startLon = startLon;
        _rng = new Random(seed);
    }

    public GpsSample NextFix(long nowMs)
    {
        // Random walk keeps the route plausible.
        var lat = _startLat + (_rng.NextDouble() - 0.5) * 0.01;
        var lon = _startLon + (_rng.NextDouble() - 0.5) * 0.01;
        return new GpsSample
        {
            Lat = lat,
            Lon = lon,
            AccuracyM = 5 + _rng.NextDouble() * 8,
            AtUnixMs = (ulong)nowMs,
        };
    }
}
