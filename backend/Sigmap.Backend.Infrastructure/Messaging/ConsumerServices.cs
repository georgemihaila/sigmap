using Microsoft.Extensions.DependencyInjection;
using Sigmap.Contracts.Messaging;
using Microsoft.Extensions.Logging;
using Sigmap.Backend.Application.Configuration;
using Sigmap.Backend.Application.Ingestion;
using Sigmap.Backend.Infrastructure.Ingestion;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Infrastructure.Messaging;

public sealed class IngestConsumerService : RabbitMqConsumerService
{
    private readonly IServiceScopeFactory _scopes;

    public IngestConsumerService(
        RabbitMqConnectionProvider connection,
        IServiceScopeFactory scopes,
        ILogger<IngestConsumerService> log)
        : base(connection, log)
    {
        _scopes = scopes;
    }

    protected override string Queue => RabbitMqTopology.DetectionBatchQueue;

    protected override async Task HandleAsync(Envelope envelope, CancellationToken ct)
    {
        if (envelope.PayloadCase != Envelope.PayloadOneofCase.DetectionBatch)
            return;

        await using var scope = _scopes.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IDetectionBatchProcessor>();
        var result = await processor.ProcessAsync(envelope.DetectionBatch, ct);
        if (result.Status == IngestStatus.Rejected)
            throw new InvalidOperationException($"Batch {envelope.DetectionBatch.BatchId} rejected");
    }
}

public sealed class HeartbeatConsumerService : RabbitMqConsumerService
{
    private readonly IServiceScopeFactory _scopes;

    public HeartbeatConsumerService(
        RabbitMqConnectionProvider connection,
        IServiceScopeFactory scopes,
        ILogger<HeartbeatConsumerService> log)
        : base(connection, log)
    {
        _scopes = scopes;
    }

    protected override string Queue => RabbitMqTopology.HeartbeatQueue;

    protected override async Task HandleAsync(Envelope envelope, CancellationToken ct)
    {
        if (envelope.PayloadCase != Envelope.PayloadOneofCase.Heartbeat)
            return;

        await using var scope = _scopes.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<HeartbeatProcessor>();
        await processor.ProcessAsync(envelope.Heartbeat, ct);
    }
}

public sealed class ConfigAckConsumerService : RabbitMqConsumerService
{
    private readonly IServiceScopeFactory _scopes;

    public ConfigAckConsumerService(
        RabbitMqConnectionProvider connection,
        IServiceScopeFactory scopes,
        ILogger<ConfigAckConsumerService> log)
        : base(connection, log)
    {
        _scopes = scopes;
    }

    protected override string Queue => RabbitMqTopology.ConfigAckQueue;

    protected override async Task HandleAsync(Envelope envelope, CancellationToken ct)
    {
        if (envelope.PayloadCase != Envelope.PayloadOneofCase.ConfigAck)
            return;

        await using var scope = _scopes.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<ConfigAckProcessor>();
        await processor.ProcessAsync(envelope.ConfigAck, ct);
    }
}
