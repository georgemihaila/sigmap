using System.Threading.Channels;
using Sigmap.Application.Abstractions;
using Sigmap.Domain.Enums;

namespace Sigmap.Infrastructure.Simulation;

/// <summary>A config push awaiting the simulated device ACK round-trip.</summary>
public sealed record PendingPush(Guid SessionId, Guid DeviceId, string PushId, DateTimeOffset AppliedAt);

public interface IConfigPushQueue
{
    void Enqueue(PendingPush push);
    ChannelReader<PendingPush> Reader { get; }
}

public sealed class ConfigPushQueue : IConfigPushQueue
{
    private readonly Channel<PendingPush> _channel = Channel.CreateUnbounded<PendingPush>();
    public void Enqueue(PendingPush push) => _channel.Writer.TryWrite(push);
    public ChannelReader<PendingPush> Reader => _channel.Reader;
}
