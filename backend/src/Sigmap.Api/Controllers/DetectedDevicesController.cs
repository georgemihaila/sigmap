using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigmap.Application.Dtos;
using Sigmap.Application.Services;

namespace Sigmap.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class DetectedDevicesController(IDetectedService detected) : ControllerBase
{
    [HttpGet("detected-devices")]
    public Task<DetectedDevicePageDto> List(
        [FromQuery] string? cursor,
        [FromQuery] int limit = 50,
        [FromQuery] string? deviceType = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default) =>
        detected.ListAsync(new DetectedQuery(cursor, limit, deviceType, search), ct);

    [HttpGet("detected-devices/map")]
    public Task<List<DetectedDeviceDto>> Map(CancellationToken ct) => detected.ListLocatedAsync(ct);

    [HttpGet("devices/detected/{mac}/signal-series")]
    public Task<List<SignalPointDto>> SignalSeries(string mac, CancellationToken ct) =>
        detected.GetSignalSeriesAsync(mac, ct);

    [HttpGet("devices/detected/{mac}")]
    public async Task<IActionResult> Get(string mac, CancellationToken ct)
    {
        var device = await detected.GetByMacAsync(mac, ct);
        return device is null ? NotFound(new { error = "not_found" }) : Ok(device);
    }
}
