using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sigmap.Application.Abstractions;

namespace Sigmap.Infrastructure.Live;

public sealed class InMemoryLiveEventBus(ILogger<InMemoryLiveEventBus> logger) : ILiveEventBus
{
    private readonly ConcurrentDictionary<Guid, List<Action<LiveEvent>>> _subscribers = new();

    public IDisposable Subscribe(Guid sessionId, Action<LiveEvent> listener)
    {
        _subscribers.AddOrUpdate(sessionId,
            _ => [listener],
            (_, list) =>
            {
                lock (list) list.Add(listener);
                return list;
            });

        return new Subscription(this, sessionId, listener);
    }

    public void Publish(LiveEvent @event)
    {
        if (!_subscribers.TryGetValue(@event.SessionId, out var listeners)) return;
        Action<LiveEvent>[] snapshot;
        lock (listeners) snapshot = listeners.ToArray();
        foreach (var listener in snapshot)
        {
            try
            {
                listener(@event);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Live event listener failed for session {SessionId}", @event.SessionId);
            }
        }
    }

    private void Remove(Guid sessionId, Action<LiveEvent> listener)
    {
        if (!_subscribers.TryGetValue(sessionId, out var list)) return;
        lock (list)
        {
            list.Remove(listener);
            if (list.Count == 0) _subscribers.TryRemove(sessionId, out _);
        }
    }

    private sealed class Subscription(InMemoryLiveEventBus bus, Guid sessionId, Action<LiveEvent> listener) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            bus.Remove(sessionId, listener);
        }
    }
}
