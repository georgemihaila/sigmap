using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Sigmap.Backend.Application.Exports;
using Sigmap.Backend.Infrastructure.Persistence;

namespace Sigmap.Backend.Api.Endpoints;

public static class ExportEndpoints
{
    public static RouteGroupBuilder MapExportEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/exports", List);
        api.MapPost("/exports", Create);
        api.MapGet("/exports/{id:guid}/download", Download);
        return api;
    }

    private static async Task<Ok<IReadOnlyList<ExportRecordDto>>> List(IExportService service, CancellationToken ct)
    {
        return TypedResults.Ok(await service.ListExportsAsync(ct));
    }

    private static async Task<Results<Ok<ExportRecordDto>, BadRequest<string>>> Create(
        CreateExportRequest req, IExportService service, CancellationToken ct)
    {
        try
        {
            return TypedResults.Ok(await service.CreateExportAsync(req.SessionId, req.Format, ct));
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<FileStreamHttpResult, NotFound>> Download(
        Guid id, SigmapDbContext db, CancellationToken ct)
    {
        var record = await db.Exports.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (record?.FilePath is null || !File.Exists(record.FilePath))
            return TypedResults.NotFound();
        var stream = File.OpenRead(record.FilePath);
        return TypedResults.File(stream, "text/csv");
    }

    public sealed record CreateExportRequest(Guid? SessionId, string Format);
}
