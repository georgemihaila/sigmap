using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Sigmap.Backend.Domain.Entities;
using Sigmap.Backend.Infrastructure.Persistence;

namespace Sigmap.Backend.Api.Endpoints;

public static class PresetEndpoints
{
    public static RouteGroupBuilder MapPresetEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/presets");

        g.MapGet("/", ListPresets);
        g.MapGet("/builtin", ListBuiltinPresets);
        g.MapPost("/", CreatePreset);
        g.MapPut("/{id:guid}", UpdatePreset);
        g.MapDelete("/{id:guid}", DeletePreset);

        return g;
    }

    private static async Task<Ok<List<ScanPreset>>> ListPresets(SigmapDbContext db, CancellationToken ct)
    {
        var presets = await db.ScanPresets
            .Where(p => !p.IsBuiltin)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);
        return TypedResults.Ok(presets);
    }

    private static async Task<Ok<List<ScanPreset>>> ListBuiltinPresets(SigmapDbContext db, CancellationToken ct)
    {
        var presets = await db.ScanPresets.Where(p => p.IsBuiltin).OrderBy(p => p.Name).ToListAsync(ct);
        return TypedResults.Ok(presets);
    }

    private static async Task<Created<ScanPreset>> CreatePreset(ScanPreset preset, SigmapDbContext db, CancellationToken ct)
    {
        preset.Id = Guid.NewGuid();
        preset.IsBuiltin = false;
        preset.CreatedAt = DateTimeOffset.UtcNow;
        preset.UpdatedAt = preset.CreatedAt;
        db.ScanPresets.Add(preset);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/presets/{preset.Id}", preset);
    }

    private static async Task<Results<Ok<ScanPreset>, NotFound>> UpdatePreset(
        Guid id, ScanPreset update, SigmapDbContext db, CancellationToken ct)
    {
        var preset = await db.ScanPresets.FirstOrDefaultAsync(p => p.Id == id && !p.IsBuiltin, ct);
        if (preset is null)
            return TypedResults.NotFound();
        preset.Name = update.Name;
        preset.Description = update.Description;
        preset.ConfigJson = update.ConfigJson;
        preset.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(preset);
    }

    private static async Task<Results<NoContent, NotFound>> DeletePreset(Guid id, SigmapDbContext db, CancellationToken ct)
    {
        var preset = await db.ScanPresets.FirstOrDefaultAsync(p => p.Id == id && !p.IsBuiltin, ct);
        if (preset is null)
            return TypedResults.NotFound();
        db.ScanPresets.Remove(preset);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
