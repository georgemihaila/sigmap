namespace Sigmap.Application.Abstractions;

/// <summary>
/// In-memory fan-out for live events. The simulator publishes; the gRPC-Web
/// stream service subscribes per session.
/// </summary>
public interface ILiveEventBus
{
    /// <summary>Subscribe to a session's stream. Returns an unsubscribe action.</summary>
    IDisposable Subscribe(Guid sessionId, Action<LiveEvent> listener);

    /// <summary>Broadcast an event to everyone subscribed to the session.</summary>
    void Publish(LiveEvent @event);
}
