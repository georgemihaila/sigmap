using Microsoft.Extensions.Hosting;
using Sigmap.Contracts.Messaging;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sigmap.Contracts;
using Sigmap.Contracts.Proto;namespace Sigmap.Backend.Infrastructure.Messaging;

/// <summary>
/// Base for RabbitMQ consumers: reconnect loop, topology declaration, prefetch
/// 1, manual acks. Derived classes implement <see cref="HandleAsync"/> which
/// runs after the envelope is parsed and schema-validated. The message is acked
/// only when the handler completes; failures are nacked to the dead-letter
/// queue (never lost).
/// </summary>
public abstract class RabbitMqConsumerService : BackgroundService
{
    private readonly RabbitMqConnectionProvider _connection;
    private readonly ILogger _log;

    protected RabbitMqConsumerService(RabbitMqConnectionProvider connection, ILogger log)
    {
        _connection = connection;
        _log = log;
    }

    protected abstract string Queue { get; }
    protected abstract Task HandleAsync(Envelope envelope, CancellationToken ct);

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
                consumer.ReceivedAsync += (_, args) => ProcessAsync(channel, args, stoppingToken);
                await channel.BasicConsumeAsync(Queue, false, consumer, stoppingToken);

                _log.LogInformation("Consumer {Queue} started", Queue);

                // Wait until the channel/connection dies or we are cancelled.
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
                _log.LogError(ex, "Consumer {Queue} crashed; reconnecting in 3s", Queue);
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

    private async Task ProcessAsync(IChannel channel, BasicDeliverEventArgs args, CancellationToken ct)
    {
        try
        {
            var envelope = Envelope.Parser.ParseFrom(args.Body.ToArray());
            if (envelope.SchemaVersion != Schema.Version)
                throw new InvalidOperationException(
                    $"Unsupported schema version '{envelope.SchemaVersion}' (expected '{Schema.Version}')");

            await HandleAsync(envelope, ct);
            await channel.BasicAckAsync(args.DeliveryTag, false, ct);
        }
        catch (OperationCanceledException)
        {
            // On shutdown, let the message be requeued rather than lost.
            await channel.BasicNackAsync(args.DeliveryTag, false, true, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Consumer {Queue} failed to process {RoutingKey}", Queue, args.RoutingKey);
            await channel.BasicNackAsync(args.DeliveryTag, false, false, ct);
        }
    }
}
