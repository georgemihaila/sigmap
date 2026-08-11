using Google.Protobuf;
using Microsoft.AspNetCore.Http.HttpResults;
using Sigmap.Backend.Application.Pairing;
using Sigmap.Contracts;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Api.Endpoints;

public static class PairingEndpoints
{
    public static RouteGroupBuilder MapPairingEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/pairing");
        g.MapPost("/request", Request);
        g.MapGet("/pending", Pending);
        g.MapGet("/qr/{sessionId:guid}", Qr);
        g.MapPost("/{deviceId:guid}/approve", Approve);
        g.MapPost("/{deviceId:guid}/reject", Reject);
        return g;
    }

    /// <summary>Accepts a protobuf or JSON PairingRequest body.</summary>
    private static async Task<Results<Ok<PairingResponse>, BadRequest<string>>> Request(
        HttpRequest request, IPairingService service, ILoggerFactory logFactory, CancellationToken ct)
    {
        PairingRequest pairing;
        try
        {
            if (request.ContentType?.Contains("protobuf", StringComparison.OrdinalIgnoreCase) == true)
            {
                pairing = PairingRequest.Parser.ParseFrom(request.Body);
            }
            else
            {
                pairing = PairingRequest.Parser.ParseJson(await new StreamReader(request.Body).ReadToEndAsync(ct));
            }
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Malformed pairing request: {ex.Message}");
        }

        var response = await service.RequestAsync(pairing, request.HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return TypedResults.Ok(response);
    }

    private static async Task<Ok<List<Domain.Entities.Device>>> Pending(
        IPairingService service, CancellationToken ct)
    {
        return TypedResults.Ok((await service.PendingAsync(ct)).ToList());
    }

    private static async Task<Ok<string>> Qr(Guid sessionId, IPairingService service, CancellationToken ct)
    {
        return TypedResults.Ok(service.BuildQrPayload(sessionId));
    }

    private static async Task<Results<Ok<Domain.Entities.Device>, NotFound, BadRequest<string>>> Approve(
        Guid deviceId, ApproveRequest req, IPairingService service, CancellationToken ct)
    {
        try
        {
            var device = await service.ApproveAsync(deviceId, req.SessionId, ct);
            return TypedResults.Ok(device);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<NoContent> Reject(Guid deviceId, IPairingService service, CancellationToken ct)
    {
        await service.RejectAsync(deviceId, ct);
        return TypedResults.NoContent();
    }

    public sealed record ApproveRequest(Guid SessionId);
}
