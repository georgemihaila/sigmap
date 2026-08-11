using Sigmap.Scanner.Agent.ChannelHop;

namespace Sigmap.Scanner.Tests;

public class ChannelHopperTests
{
    [Fact]
    public void Next_cycles_channels_in_order_and_wraps()
    {
        var hopper = new ChannelHopper(new[] { 1, 6, 11 }, TimeSpan.FromMilliseconds(100));
        Assert.Equal(1, hopper.CurrentChannel);
        Assert.Equal(6, hopper.Next());
        Assert.Equal(11, hopper.Next());
        Assert.Equal(1, hopper.Next());
        Assert.Equal(6, hopper.Next());
    }

    [Fact]
    public async Task HopLoop_applies_each_channel_and_dwells_between_hops()
    {
        var applied = new List<int>();
        var delays = new List<TimeSpan>();
        var hopper = new ChannelHopper(new[] { 1, 6, 11 }, TimeSpan.FromMilliseconds(50), (t, ct) =>
        {
            delays.Add(t);
            return Task.Delay(t, ct); // real delay observes cancellation
        });

        using var cts = new CancellationTokenSource();
        var loop = hopper.HopLoopAsync((ch, _) =>
        {
            applied.Add(ch);
            if (applied.Count == 4)
                cts.Cancel();
            return Task.CompletedTask;
        }, cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loop);

        // channels 1, 6, 11, then wraps back to 1
        Assert.Equal(new[] { 1, 6, 11, 1 }, applied);
        // one dwell per hop applied
        Assert.Equal(applied.Count, delays.Count);
        Assert.All(delays, d => Assert.Equal(TimeSpan.FromMilliseconds(50), d));
    }

    [Fact]
    public void Empty_channels_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new ChannelHopper(Array.Empty<int>(), TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public void Single_channel_stays_on_that_channel()
    {
        var hopper = new ChannelHopper(new[] { 9 }, TimeSpan.FromMilliseconds(10));
        Assert.Equal(9, hopper.CurrentChannel);
        Assert.Equal(9, hopper.Next());
    }
}
