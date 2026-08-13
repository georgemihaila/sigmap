using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigmap.Application.Dtos;
using Sigmap.Application.Services;

namespace Sigmap.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sessions")]
public class SessionsController(ISessionService sessions, IFleetService fleet) : ControllerBase
{
    [HttpGet]
    public Task<List<SessionDto>> List(CancellationToken ct) => sessions.ListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var session = await sessions.GetAsync(id, ct);
        return session is null ? NotFound(new { error = "not_found" }) : Ok(session);
    }

    [HttpPost]
    [Authorize(Roles = "operator")]
    public async Task<IActionResult> Create([FromBody] SessionInputDto input, CancellationToken ct)
    {
        var session = await sessions.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id = session.Id }, session);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "operator")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SessionInputDto input, CancellationToken ct)
    {
        var session = await sessions.UpdateAsync(id, input, ct);
        return session is null ? NotFound(new { error = "not_found" }) : Ok(session);
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Roles = "operator")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var session = await sessions.ArchiveAsync(id, ct);
        return session is null ? NotFound(new { error = "not_found" }) : Ok(session);
    }

    [HttpGet("{id:guid}/stats")]
    public Task<SessionStatsDto> Stats(Guid id, CancellationToken ct) => sessions.GetStatsAsync(id, ct);

    [HttpGet("{id:guid}/swarms")]
    public Task<List<SwarmDto>> Swarms(Guid id, CancellationToken ct) => sessions.ListSwarmsAsync(id, ct);

    [HttpGet("{id:guid}/coverage")]
    public Task<CoverageDto> Coverage(Guid id, CancellationToken ct, [FromQuery] int limit = 5000) =>
        sessions.GetCoverageAsync(id, Math.Clamp(limit, 1, 100_000), ct);

    [HttpGet("{id:guid}/fleet")]
    public Task<List<FleetDeviceDto>> SessionFleet(Guid id, CancellationToken ct) => fleet.ListAsync(id, ct);

    [HttpGet("{id:guid}/live")]
    public async Task<IActionResult> Live(Guid id, CancellationToken ct)
    {
        if (await sessions.GetAsync(id, ct) is null)
            return NotFound(new { error = "not_found" });
        return Ok(await sessions.GetLiveSnapshotAsync(id, ct));
    }
}
