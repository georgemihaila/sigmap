using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sigmap.Contracts;
using Sigmap.Contracts.Messages;
using Sigmap.Contracts.Messaging;
using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent.Broker;

/// <summary>
/// Subscribes to the device's durable config queue, applies ConfigPush messages
/// (updating the live config) and acks back APPLIED/REJECTED/FAILED so the
/// backend's push-state tracking converges.
/// </summary>
public sealed class ConfigConsumer
{
    private readonly string _deviceId;
    private readonly Action<ScanConfig> _apply;
    private readonly RabbitMqPublisher _publisher;
    private readonly ILogger<ConfigConsumer> _log;

    public ConfigConsumer(
        string deviceId,
        Action<ScanConfig> apply,
        RabbitMqPublisher publisher,
        ILogger<ConfigConsumer> log)
    {
        _deviceId = deviceId;
        _apply = apply;
        _publisher = publisher;
        _log = log;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory { Uri = new Uri(_publisher.ConnectionString) };
                var connection = await factory.CreateConnectionAsync("sigmap-scanner-config", ct);
                await using var channel = await connection.CreateChannelAsync(null, ct);
                await RabbitMqTopology.DeclareAsync(channel, ct);
                await channel.QueueDeclareAsync(RabbitMqTopology.ConfigQueue(_deviceId), true, false, false, null, false, false, ct);
                await channel.QueueBindAsync(
                    RabbitMqTopology.ConfigQueue(_deviceId),
                    RabbitMqTopology.ConfigExchange,
                    RabbitMqTopology.ConfigKey(_deviceId),
                    null,
                    false,
                    ct);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, args) => HandleAsync(channel, args, ct);
                await channel.BasicConsumeAsync(RabbitMqTopology.ConfigQueue(_deviceId), false, consumer, ct);
                _log.LogInformation("Config consumer listening on {Queue}", RabbitMqTopology.ConfigQueue(_deviceId));

                var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                channel.ChannelShutdownAsync += (_, _) =>
                {
                    done.TrySetResult();
                    return Task.CompletedTask;
                };
                using var reg = ct.Register(() => done.TrySetCanceled(ct));
                await done.Task;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Config consumer crashed; reconnecting in 3s");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs args, CancellationToken ct)
    {
        try
        {
            var envelope = Envelope.Parser.ParseFrom(args.Body.ToArray());
            if (envelope.SchemaVersion != Schema.Version || envelope.PayloadCase != Envelope.PayloadOneofCase.ConfigPush)
                throw new InvalidOperationException($"Unexpected message on config queue (version={envelope.SchemaVersion}, payload={envelope.PayloadCase})");

            var push = envelope.ConfigPush;
            var status = ConfigAckStatus.Applied;
            var error = string.Empty;
            try
            {
                _apply(push.Config);
                _log.LogInformation("Applied config push {PushId} (rev {PresetId})", push.PushId, string.IsNullOrEmpty(push.PresetId) ? "custom" : $"preset {push.PresetId}");
            }
            catch (Exception ex)
            {
                status = ConfigAckStatus.Failed;
                error = ex.Message;
                _log.LogError(ex, "Config push {PushId} failed", push.PushId);
            }

            var ack = EnvelopeFactory.For(push.PushId, new ConfigAck
            {
                PushId = push.PushId,
                DeviceId = _deviceId,
                Status = status,
                Error = error,
                AppliedAtUnixMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
            await _publisher.PublishAsync(
                RabbitMqTopology.IngestExchange,
                RabbitMqTopology.ConfigAckKeyPrefix + _deviceId,
                ack,
                persistent: true,
                ct);

            await channel.BasicAckAsync(args.DeliveryTag, false, ct);
        }
        catch (OperationCanceledException)
        {
            await channel.BasicNackAsync(args.DeliveryTag, false, true, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Config message rejected");
            await channel.BasicNackAsync(args.DeliveryTag, false, false, ct);
        }
    }
}
