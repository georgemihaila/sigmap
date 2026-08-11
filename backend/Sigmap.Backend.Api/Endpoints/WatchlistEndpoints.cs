using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Sigmap.Backend.Domain.Entities;
using Sigmap.Backend.Infrastructure.Persistence;

namespace Sigmap.Backend.Api.Endpoints;

public static class WatchlistEndpoints
{
    public static RouteGroupBuilder MapWatchlistEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/watchlist");
        g.MapGet("/", List);
        g.MapPost("/", Create);
        g.MapPut("/{id:guid}", Update);
        g.MapDelete("/{id:guid}", Delete);
        return g;
    }

    private static async Task<Ok<List<WatchlistEntry>>> List(Guid? owner, SigmapDbContext db, CancellationToken ct)
    {
        var query = db.WatchlistEntries.AsNoTracking();
        if (owner is not null)
            query = query.Where(w => w.OwnerId == owner);
        var entries = await query.OrderBy(w => w.CreatedAt).ToListAsync(ct);
        return TypedResults.Ok(entries);
    }

    private static async Task<Created<WatchlistEntry>> Create(WatchlistEntry entry, SigmapDbContext db, CancellationToken ct)
    {
        entry.Id = Guid.NewGuid();
        entry.CreatedAt = DateTimeOffset.UtcNow;
        db.WatchlistEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/watchlist/{entry.Id}", entry);
    }

    private static async Task<Results<Ok<WatchlistEntry>, NotFound>> Update(
        Guid id, WatchlistEntry update, SigmapDbContext db, CancellationToken ct)
    {
        var entry = await db.WatchlistEntries.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (entry is null)
            return TypedResults.NotFound();
        entry.Mac = update.Mac;
        entry.Ssid = update.Ssid;
        entry.Tag = update.Tag;
        entry.Action = update.Action;
        entry.Notes = update.Notes;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(entry);
    }

    private static async Task<Results<NoContent, NotFound>> Delete(Guid id, SigmapDbContext db, CancellationToken ct)
    {
        var entry = await db.WatchlistEntries.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (entry is null)
            return TypedResults.NotFound();
        db.WatchlistEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
