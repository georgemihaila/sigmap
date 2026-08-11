using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Sigmap.Backend.Application.Configuration;
using Sigmap.Backend.Infrastructure.Persistence;

namespace Sigmap.Backend.Api.Endpoints;

public static class ConfigEndpoints
{
    public static RouteGroupBuilder MapConfigEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/sessions/{sessionId:guid}/devices/{deviceId:guid}/config");

        g.MapGet("/", GetConfig);
        g.MapPut("/", ApplyConfig);

        return g;
    }

    private static async Task<Results<Ok<SessionConfigDto>, NotFound>> GetConfig(
        Guid sessionId, Guid deviceId, ISessionConfigService service, CancellationToken ct)
    {
        var dto = await service.GetAsync(sessionId, deviceId, ct);
        return dto is null ? TypedResults.NotFound() : TypedResults.Ok(dto);
    }

    private static async Task<Results<Ok<SessionConfigPushResult>, NotFound>> ApplyConfig(
        Guid sessionId, Guid deviceId, ApplyConfigRequest request, ISessionConfigService service, CancellationToken ct)
    {
        var config = ScanConfigFactory.FromJson(request.Config);
        var result = await service.ApplyAsync(sessionId, deviceId, config, request.PresetId, ct);
        return TypedResults.Ok(result);
    }

    public sealed record ApplyConfigRequest(string Config, Guid? PresetId);
}
