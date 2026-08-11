using System.Threading.Channels;
using Sigmap.Contracts.Proto.Live;

namespace Sigmap.Bff.Live;

/// <summary>In-process fan-out of live events to all connected gRPC-Web streams.</summary>
public sealed class LiveEventBus : IDisposable
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, Channel<LiveEvent>> _subscribers = new();
    private readonly BoundedChannelOptions _options = new(1_000) { FullMode = BoundedChannelFullMode.DropWrite };

    /// <summary>Registers a subscriber and returns a handle that unregisters it.</summary>
    public IDisposable Subscribe(out ChannelReader<LiveEvent> reader)
    {
        var channel = Channel.CreateBounded<LiveEvent>(_options);
        var id = Guid.NewGuid();
        lock (_lock)
        {
            _subscribers[id] = channel;
        }

        reader = channel.Reader;
        return new Unsubscriber(this, id);
    }

    public void Publish(LiveEvent evt)
    {
        lock (_lock)
        {
            foreach (var channel in _subscribers.Values)
                channel.Writer.TryWrite(evt);
        }
    }

    private void Unsubscribe(Guid id)
    {
        lock (_lock)
        {
            if (_subscribers.Remove(id, out var channel))
                channel.Writer.TryComplete();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var channel in _subscribers.Values)
                channel.Writer.TryComplete();
            _subscribers.Clear();
        }
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly LiveEventBus _bus;
        private readonly Guid _id;

        public Unsubscriber(LiveEventBus bus, Guid id)
        {
            _bus = bus;
            _id = id;
        }

        public void Dispose() => _bus.Unsubscribe(_id);
    }
}
