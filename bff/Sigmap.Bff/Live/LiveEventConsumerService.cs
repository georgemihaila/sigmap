using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sigmap.Contracts;
using Sigmap.Contracts.Messaging;
using Sigmap.Contracts.Proto;
using Sigmap.Contracts.Proto.Live;

namespace Sigmap.Bff.Live;

/// <summary>Consumes the backend's internal events exchange and fans out to browsers.</summary>
public sealed class LiveEventConsumerService : BackgroundService
{
    private readonly RabbitMqConnectionProvider _connection;
    private readonly LiveEventBus _bus;
    private readonly ILogger<LiveEventConsumerService> _log;

    public LiveEventConsumerService(
        RabbitMqConnectionProvider connection, LiveEventBus bus, ILogger<LiveEventConsumerService> log)
    {
        _connection = connection;
        _bus = bus;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var conn = await _connection.GetConnectionAsync(stoppingToken);
                await using var channel = await conn.CreateChannelAsync(null, stoppingToken);
                await RabbitMqTopology.DeclareAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, args) =>
                {
                    try
                    {
                        var envelope = Envelope.Parser.ParseFrom(args.Body.ToArray());
                        if (envelope.SchemaVersion == Schema.Version && envelope.LiveEvent is not null)
                            _bus.Publish(envelope.LiveEvent);
                        await channel.BasicAckAsync(args.DeliveryTag, false, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Failed to forward live event");
                        await channel.BasicNackAsync(args.DeliveryTag, false, false, stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(RabbitMqTopology.BffQueue, false, consumer, stoppingToken);
                _log.LogInformation("Live event consumer started on {Queue}", RabbitMqTopology.BffQueue);

                var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                channel.ChannelShutdownAsync += (_, _) =>
                {
                    done.TrySetResult();
                    return Task.CompletedTask;
                };
                using var reg = stoppingToken.Register(() => done.TrySetCanceled(stoppingToken));
                await done.Task;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Live event consumer crashed; reconnecting in 3s");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
