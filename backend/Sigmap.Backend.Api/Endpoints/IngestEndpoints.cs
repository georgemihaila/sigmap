using Google.Protobuf;
using Sigmap.Contracts.Messaging;
using Microsoft.AspNetCore.Http.HttpResults;
using Sigmap.Backend.Infrastructure.Messaging;
using Sigmap.Contracts;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Api.Endpoints;

public static class IngestEndpoints
{
    public static RouteGroupBuilder MapIngestEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/ingest/batch", IngestBatch);
        return api;
    }

    /// <summary>
    /// HTTPS ingest used by the Android client (and anything without a durable
    /// AMQP session). Accepts the same protobuf <c>Envelope</c> as the broker
    /// and re-publishes it onto the ingestion exchange.
    /// </summary>
    private static async Task<Results<Accepted<object?>, BadRequest<string>>> IngestBatch(
        HttpRequest request,
        RabbitMqConnectionProvider connection,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var log = loggerFactory.CreateLogger("Ingest");
        Envelope envelope;
        try
        {
            envelope = Envelope.Parser.ParseFrom(request.Body);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Malformed envelope: {ex.Message}");
        }

        if (envelope.SchemaVersion != Schema.Version)
            return TypedResults.BadRequest($"Unsupported schema version '{envelope.SchemaVersion}'");

        if (envelope.PayloadCase != Envelope.PayloadOneofCase.DetectionBatch)
            return TypedResults.BadRequest("Payload must be a detection batch");

        var batch = envelope.DetectionBatch;
        if (string.IsNullOrEmpty(envelope.MessageId))
            envelope.MessageId = batch.BatchId;
        if (string.IsNullOrEmpty(envelope.SchemaVersion))
            envelope.SchemaVersion = Schema.Version;

        await connection.PublishAsync(
            RabbitMqTopology.IngestExchange,
            RabbitMqTopology.DetectionBatchKeyPrefix + batch.DeviceId,
            envelope.ToByteArray(),
            persistent: true,
            ct);

        log.LogInformation("HTTPS ingest accepted batch {BatchId} from {DeviceId}", batch.BatchId, batch.DeviceId);
        return TypedResults.Accepted<object?>("~/api/v1/ingest/batch", null);
    }
}
