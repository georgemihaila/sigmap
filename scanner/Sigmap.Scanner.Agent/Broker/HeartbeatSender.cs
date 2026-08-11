using Microsoft.Extensions.Logging;
using Sigmap.Scanner.Agent.Broker;
using Sigmap.Scanner.Agent.Buffer;
using Sigmap.Contracts.Messages;
using Sigmap.Contracts.Messaging;
using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent.Broker;

/// <summary>Periodic fleet heartbeat published to sigmap.heartbeat.</summary>
public sealed class HeartbeatSender
{
    private readonly RabbitMqPublisher _publisher;
    private readonly string _deviceId;
    private readonly TimeSpan _interval;
    private readonly DetectionBuffer _buffer;
    private readonly Func<long> _sentTotal;
    private readonly ILogger<HeartbeatSender> _log;

    public HeartbeatSender(
        RabbitMqPublisher publisher,
        string deviceId,
        TimeSpan interval,
        DetectionBuffer buffer,
        Func<long> sentTotal,
        ILogger<HeartbeatSender> log)
    {
        _publisher = publisher;
        _deviceId = deviceId;
        _interval = interval;
        _buffer = buffer;
        _sentTotal = sentTotal;
        _log = log;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var heartbeat = new Heartbeat
                {
                    DeviceId = _deviceId,
                    AtUnixMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Status = DeviceStatus.Busy,
                    DetectionsBuffered = (ulong)_buffer.Count,
                    DetectionsSentTotal = (ulong)_sentTotal(),
                    GpsFix = false,
                };
                var envelope = EnvelopeFactory.For($"hb-{Guid.NewGuid():N}", heartbeat);
                await _publisher.PublishAsync(
                    RabbitMqTopology.HeartbeatExchange,
                    RabbitMqTopology.HeartbeatKeyPrefix + _deviceId,
                    envelope,
                    persistent: false,
                    ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Heartbeat publish failed");
            }

            try
            {
                await Task.Delay(_interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
