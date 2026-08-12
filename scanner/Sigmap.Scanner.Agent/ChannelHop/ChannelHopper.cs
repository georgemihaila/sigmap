namespace Sigmap.Scanner.Agent.ChannelHop;

/// <summary>
/// Cycles through a channel list with a dwell time. The hop loop applies each
/// channel via an injected callback (e.g. <c>iw dev ... set channel</c>) and
/// dwells before moving on. The channel sequence itself is pure, so it is
/// directly unit-testable.
/// </summary>
public sealed class ChannelHopper
{
    private readonly IReadOnlyList<int> _channels;
    private readonly TimeSpan _dwell;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private int _index;

    public ChannelHopper(IReadOnlyList<int> channels, TimeSpan dwell)
        : this(channels, dwell, static (t, ct) => Task.Delay(t, ct))
    {
    }

    internal ChannelHopper(IReadOnlyList<int> channels, TimeSpan dwell, Func<TimeSpan, CancellationToken, Task> delay)
    {
        if (channels.Count == 0)
            throw new ArgumentException("At least one channel is required", nameof(channels));
        _channels = channels;
        _dwell = dwell;
        _delay = delay;
    }

    public int CurrentChannel => _channels[_index];

    public int FirstChannel => _channels[0];

    public int ChannelCount => _channels.Count;

    /// <summary>Advances to the next channel (wraps around).</summary>
    public int Next()
    {
        _index = (_index + 1) % _channels.Count;
        return _channels[_index];
    }

    /// <summary>
    /// Applies each channel via <paramref name="onHop"/> and dwells the
    /// configured interval between hops, until cancelled.
    /// </summary>
    public async Task HopLoopAsync(Func<int, CancellationToken, Task> onHop, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await onHop(_channels[_index], ct);
            await _delay(_dwell, ct);
            Next();
        }
    }
}
