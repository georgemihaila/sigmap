using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Sigmap.Backend.Domain.Entities;
using Sigmap.Backend.Infrastructure.Persistence;

namespace Sigmap.Backend.Api.Endpoints;

public static class AlertEndpoints
{
    public static RouteGroupBuilder MapAlertEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/alerts");
        g.MapGet("/rules", ListRules);
        g.MapPost("/rules", CreateRule);
        g.MapPut("/rules/{id:guid}", UpdateRule);
        g.MapDelete("/rules/{id:guid}", DeleteRule);
        return g;
    }

    private static async Task<Ok<List<AlertRule>>> ListRules(SigmapDbContext db, CancellationToken ct)
    {
        var rules = await db.AlertRules.AsNoTracking().OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return TypedResults.Ok(rules);
    }

    private static async Task<Created<AlertRule>> CreateRule(AlertRule rule, SigmapDbContext db, CancellationToken ct)
    {
        rule.Id = Guid.NewGuid();
        rule.CreatedAt = DateTimeOffset.UtcNow;
        db.AlertRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/alerts/rules/{rule.Id}", rule);
    }

    private static async Task<Results<Ok<AlertRule>, NotFound>> UpdateRule(
        Guid id, AlertRule update, SigmapDbContext db, CancellationToken ct)
    {
        var rule = await db.AlertRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
            return TypedResults.NotFound();
        rule.RuleType = update.RuleType;
        rule.ConfigJson = update.ConfigJson;
        rule.Enabled = update.Enabled;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(rule);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteRule(Guid id, SigmapDbContext db, CancellationToken ct)
    {
        var rule = await db.AlertRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
            return TypedResults.NotFound();
        db.AlertRules.Remove(rule);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
