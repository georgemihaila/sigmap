using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Sigmap.Backend.Application.Exports;
using Sigmap.Backend.Domain.Entities;
using Sigmap.Backend.Infrastructure.Persistence;

namespace Sigmap.Backend.Api.Endpoints;

public static class WigleEndpoints
{
    public static RouteGroupBuilder MapWigleEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/settings/wigle", GetSettings);
        api.MapPut("/settings/wigle", PutSettings);
        api.MapPost("/wigle/upload", Upload);
        api.MapPost("/wigle/import", Import);
        return api;
    }

    private static async Task<Ok<WigleSettingsDto?>> GetSettings(SigmapDbContext db, CancellationToken ct)
    {
        var cred = await db.WigleCredentials.AsNoTracking()
            .OrderByDescending(c => c.IsDefault).FirstOrDefaultAsync(ct);
        if (cred is null)
            return TypedResults.Ok<WigleSettingsDto?>(null);
        return TypedResults.Ok<WigleSettingsDto?>(new WigleSettingsDto(
            cred.ApiName, !string.IsNullOrEmpty(cred.ApiKey), cred.Username, !string.IsNullOrEmpty(cred.Password)));
    }

    private static async Task<Ok<WigleSettingsDto>> PutSettings(
        WigleSettingsRequest req, SigmapDbContext db, CancellationToken ct)
    {
        var cred = await db.WigleCredentials.OrderByDescending(c => c.IsDefault).FirstOrDefaultAsync(ct);
        if (cred is null)
        {
            cred = new WigleCredential { IsDefault = true };
            db.WigleCredentials.Add(cred);
        }

        cred.ApiName = req.ApiName ?? cred.ApiName;
        if (!string.IsNullOrEmpty(req.ApiKey))
            cred.ApiKey = req.ApiKey;
        cred.Username = req.Username ?? cred.Username;
        if (!string.IsNullOrEmpty(req.Password))
            cred.Password = req.Password;
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(new WigleSettingsDto(cred.ApiName, true, cred.Username, true));
    }

    private static async Task<Results<Ok<WigleUploadResult>, BadRequest<string>>> Upload(
        WigleUploadRequest req, IExportService service, CancellationToken ct)
    {
        if (req.SessionId is null)
            return TypedResults.BadRequest("sessionId is required");
        return TypedResults.Ok(await service.UploadSessionToWigleAsync(req.SessionId.Value, ct));
    }

    private static async Task<Results<Ok<int>, BadRequest<string>>> Import(
        IExportService service, CancellationToken ct)
    {
        try
        {
            return TypedResults.Ok(await service.ImportFromWigleAsync(ct));
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    public sealed record WigleUploadRequest(Guid? SessionId);
    public sealed record WigleSettingsRequest(string? ApiName, string? ApiKey, string? Username, string? Password);
    public sealed record WigleSettingsDto(string? ApiName, bool ApiKeySet, string? Username, bool PasswordSet);
}
