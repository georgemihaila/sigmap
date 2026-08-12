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

        var fleet = api.MapGroup("/sessions/{sessionId:guid}/devices/config");
        fleet.MapPut("/", ApplyFleetConfig);

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

    private static async Task<Results<Ok<FleetConfigApplyResult>, NotFound>> ApplyFleetConfig(
        Guid sessionId, ApplyConfigRequest request, ISessionConfigService service,
        SigmapDbContext db, CancellationToken ct)
    {
        var sessionExists = await db.Sessions.AnyAsync(s => s.Id == sessionId, ct);
        if (!sessionExists)
            return TypedResults.NotFound();

        var deviceIds = await db.SessionDevices
            .Where(sd => sd.SessionId == sessionId)
            .Select(sd => sd.DeviceId)
            .ToListAsync(ct);

        var config = ScanConfigFactory.FromJson(request.Config);
        var results = new List<SessionConfigPushResult>();
        foreach (var deviceId in deviceIds)
        {
            results.Add(await service.ApplyAsync(sessionId, deviceId, config, request.PresetId, ct));
        }

        return TypedResults.Ok(new FleetConfigApplyResult(deviceIds.Count, results));
    }

    public sealed record FleetConfigApplyResult(int DeviceCount, List<SessionConfigPushResult> Results);
}
