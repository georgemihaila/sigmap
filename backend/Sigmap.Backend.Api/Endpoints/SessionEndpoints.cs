using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Sigmap.Backend.Domain;
using Sigmap.Backend.Domain.Entities;
using Sigmap.Backend.Infrastructure.Persistence;

namespace Sigmap.Backend.Api.Endpoints;

public static class SessionEndpoints
{
    public static RouteGroupBuilder MapSessionEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/sessions");

        g.MapGet("/", ListSessions);
        g.MapPost("/", CreateSession);
        g.MapGet("/{id:guid}", GetSession);
        g.MapPut("/{id:guid}", UpdateSession);
        g.MapDelete("/{id:guid}", DeleteSession);
        g.MapPost("/{id:guid}/archive", ArchiveSession);
        g.MapPost("/{id:guid}/swarms", CreateSwarm);
        g.MapPut("/{id:guid}/swarms/{swarmId:guid}", UpdateSwarm);
        g.MapPost("/{id:guid}/devices/{deviceId:guid}", AssignDevice);
        g.MapDelete("/{id:guid}/devices/{deviceId:guid}", UnassignDevice);

        return g;
    }

    private static async Task<Results<Ok<List<Session>>, NotFound>> ListSessions(SigmapDbContext db, CancellationToken ct)
    {
        var sessions = await db.Sessions
            .OrderByDescending(s => s.CreatedAt)
            .Include(s => s.Swarms)
            .ToListAsync(ct);
        return TypedResults.Ok(sessions);
    }

    private static async Task<Created<Session>> CreateSession(Session session, SigmapDbContext db, CancellationToken ct)
    {
        session.Id = Guid.NewGuid();
        session.CreatedAt = DateTimeOffset.UtcNow;
        session.UpdatedAt = session.CreatedAt;
        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/sessions/{session.Id}", session);
    }

    private static async Task<Results<Ok<Session>, NotFound>> GetSession(Guid id, SigmapDbContext db, CancellationToken ct)
    {
        var session = await db.Sessions
            .Include(s => s.Swarms)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        return session is null ? TypedResults.NotFound() : TypedResults.Ok(session);
    }

    private static async Task<Results<Ok<Session>, NotFound>> UpdateSession(
        Guid id, Session update, SigmapDbContext db, CancellationToken ct)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null)
            return TypedResults.NotFound();

        session.Name = update.Name;
        session.Description = update.Description;
        session.StartsAt = update.StartsAt;
        session.EndsAt = update.EndsAt;
        session.BoundingArea = update.BoundingArea;
        session.Status = update.Status;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(session);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteSession(Guid id, SigmapDbContext db, CancellationToken ct)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null)
            return TypedResults.NotFound();
        db.Sessions.Remove(session);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<Session>, NotFound>> ArchiveSession(Guid id, SigmapDbContext db, CancellationToken ct)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null)
            return TypedResults.NotFound();
        session.Status = SessionStatus.Archived;
        session.EndsAt ??= DateTimeOffset.UtcNow;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(session);
    }

    private static async Task<Results<Created<Swarm>, NotFound>> CreateSwarm(
        Guid id, Swarm swarm, SigmapDbContext db, CancellationToken ct)
    {
        var session = await db.Sessions.AnyAsync(s => s.Id == id, ct);
        if (!session)
            return TypedResults.NotFound();
        swarm.Id = Guid.NewGuid();
        swarm.SessionId = id;
        swarm.CreatedAt = DateTimeOffset.UtcNow;
        db.Swarms.Add(swarm);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/sessions/{id}/swarms/{swarm.Id}", swarm);
    }

    private static async Task<Results<Ok<Swarm>, NotFound>> UpdateSwarm(
        Guid id, Guid swarmId, Swarm update, SigmapDbContext db, CancellationToken ct)
    {
        var swarm = await db.Swarms.FirstOrDefaultAsync(s => s.Id == swarmId && s.SessionId == id, ct);
        if (swarm is null)
            return TypedResults.NotFound();
        swarm.Name = update.Name;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(swarm);
    }

    private static async Task<Results<Ok<SessionDevice>, NotFound>> AssignDevice(
        Guid id, Guid deviceId, AssignDeviceRequest req, SigmapDbContext db, CancellationToken ct)
    {
        var session = await db.Sessions.AnyAsync(s => s.Id == id, ct);
        var device = await db.Devices.AnyAsync(d => d.Id == deviceId, ct);
        if (!session || !device)
            return TypedResults.NotFound();

        var row = await db.SessionDevices
            .FirstOrDefaultAsync(sd => sd.SessionId == id && sd.DeviceId == deviceId, ct);
        if (row is null)
        {
            row = new SessionDevice { SessionId = id, DeviceId = deviceId, JoinedAt = DateTimeOffset.UtcNow };
            db.SessionDevices.Add(row);
        }
        row.SwarmId = req.SwarmId;
        row.Role = req.Role ?? row.Role;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(row);
    }

    private static async Task<Results<NoContent, NotFound>> UnassignDevice(
        Guid id, Guid deviceId, SigmapDbContext db, CancellationToken ct)
    {
        var row = await db.SessionDevices
            .FirstOrDefaultAsync(sd => sd.SessionId == id && sd.DeviceId == deviceId, ct);
        if (row is null)
            return TypedResults.NotFound();
        db.SessionDevices.Remove(row);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    public sealed record AssignDeviceRequest(Guid? SwarmId, string? Role);
}
