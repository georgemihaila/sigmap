using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigmap.Application.Dtos;
using Sigmap.Application.Services;

namespace Sigmap.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sessions/{sessionId:guid}")]
public class ConfigController(IConfigService config) : ControllerBase
{
    [HttpGet("devices/{deviceId:guid}/config")]
    public async Task<IActionResult> GetDeviceConfig(Guid sessionId, Guid deviceId, CancellationToken ct)
    {
        var dto = await config.GetDeviceConfigAsync(sessionId, deviceId, ct);
        return dto is null ? NotFound(new { error = "no_config" }) : Ok(dto);
    }

    [HttpGet("devices/{deviceId:guid}/config/push-status")]
    public Task<PushStatusDto> PushStatus(Guid sessionId, Guid deviceId, CancellationToken ct) =>
        config.GetPushStatusAsync(sessionId, deviceId, ct);

    [HttpPut("devices/{deviceId:guid}/config")]
    [Authorize(Roles = "operator")]
    public async Task<IActionResult> ApplyDeviceConfig(
        Guid sessionId,
        Guid deviceId,
        [FromBody] ApplyDeviceConfigRequest body,
        CancellationToken ct)
    {
        try
        {
            var result = await config.ApplyToDeviceAsync(sessionId, deviceId, body.ConfigJson, body.PresetId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "not_found" });
        }
    }

    [HttpPost("config/preview")]
    public async Task<IActionResult> Preview(Guid sessionId, [FromBody] PreviewRequest body, CancellationToken ct)
    {
        var diffs = await config.PreviewSessionAsync(sessionId, body.ConfigJson, ct);
        return Ok(diffs);
    }

    [HttpPut("config")]
    [Authorize(Roles = "operator")]
    public async Task<IActionResult> ApplySession(Guid sessionId, [FromBody] ApplyConfigRequest body, CancellationToken ct)
    {
        var results = await config.ApplyToManyAsync(sessionId, body.ConfigJson, body.DeviceIds, ct);
        return Ok(results);
    }
}
