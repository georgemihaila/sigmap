using Grpc.Core;
using Sigmap.Contracts.Proto.Live;

namespace Sigmap.Bff.Live;

/// <summary>gRPC-Web server-streaming: forwards live events to connected browsers.</summary>
public sealed class LiveStreamService : LiveStream.LiveStreamBase
{
    private readonly LiveEventBus _bus;
    private readonly ILogger<LiveStreamService> _log;

    public LiveStreamService(LiveEventBus bus, ILogger<LiveStreamService> log)
    {
        _bus = bus;
        _log = log;
    }

    public override async Task Subscribe(
        SubscribeRequest request, IServerStreamWriter<LiveEvent> responseStream, ServerCallContext context)
    {
        using var _ = _bus.Subscribe(out var reader);
        _log.LogInformation("Live subscriber connected (session={Session})", request.SessionId);

        try
        {
            await foreach (var evt in reader.ReadAllAsync(context.CancellationToken))
            {
                if (Matches(request, evt))
                    await responseStream.WriteAsync(evt);
            }
        }
        catch (OperationCanceledException)
        {
            // client disconnected
        }
    }

    private static bool Matches(SubscribeRequest request, LiveEvent evt)
    {
        if (string.IsNullOrEmpty(request.SessionId))
            return true;

        return evt.EventCase switch
        {
            LiveEvent.EventOneofCase.Detections => evt.Detections.SessionId == request.SessionId,
            LiveEvent.EventOneofCase.Config => evt.Config.SessionId == request.SessionId,
            _ => true, // device/fleet events are global
        };
    }
}
