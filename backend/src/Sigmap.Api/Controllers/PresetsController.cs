using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigmap.Application.Dtos;
using Sigmap.Application.Services;

namespace Sigmap.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class PresetsController(IPresetService presets, IConfigService config) : ControllerBase
{
    [HttpGet("presets")]
    public Task<List<ScanPresetDto>> List(CancellationToken ct) => presets.ListAsync(ct);

    [HttpPost("presets")]
    [Authorize(Roles = "operator")]
    public async Task<IActionResult> Create([FromBody] PresetInputDto body, CancellationToken ct)
    {
        var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        var ownerId = Guid.TryParse(userId, out var id) ? id : Guid.Empty;
        var preset = await presets.CreateAsync(body, ownerId, ct);
        return CreatedAtAction(nameof(Get), new { id = preset.Id }, preset);
    }

    [HttpGet("presets/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var all = await presets.ListAsync(ct);
        var preset = all.FirstOrDefault(p => p.Id == id);
        return preset is null ? NotFound(new { error = "not_found" }) : Ok(preset);
    }

    [HttpPut("presets/{id:guid}")]
    [Authorize(Roles = "operator")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PresetInputDto body, CancellationToken ct)
    {
        var preset = await presets.UpdateAsync(id, body, ct);
        return preset is null ? NotFound(new { error = "not_found" }) : Ok(preset);
    }

    [HttpDelete("presets/{id:guid}")]
    [Authorize(Roles = "operator")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await presets.DeleteAsync(id, ct) ? NoContent() : NotFound(new { error = "not_found" });

    [HttpGet("sessions/{sessionId:guid}/presets/{presetId:guid}/preview")]
    public async Task<IActionResult> Preview(Guid sessionId, Guid presetId, CancellationToken ct)
    {
        try
        {
            return Ok(await config.PreviewPresetAsync(sessionId, presetId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "not_found" });
        }
    }

    [HttpPost("sessions/{sessionId:guid}/presets/{presetId:guid}/apply")]
    [Authorize(Roles = "operator")]
    public async Task<IActionResult> Apply(Guid sessionId, Guid presetId, [FromBody] ApplyPresetRequest? body, CancellationToken ct)
    {
        try
        {
            var results = await config.ApplyPresetAsync(sessionId, presetId, body?.DeviceIds, ct);
            return Ok(results);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "not_found" });
        }
    }
}
