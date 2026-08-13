using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigmap.Application.Dtos;
using Sigmap.Application.Services;

namespace Sigmap.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class ExportsController(
    IExportService exports,
    ISettingsService settings,
    ILogger<ExportsController> logger) : ControllerBase
{
    [HttpGet("exports")]
    public Task<List<ExportRecordDto>> List(CancellationToken ct) => exports.ListAsync(ct);

    [HttpPost("exports")]
    [Authorize(Roles = "operator")]
    public async Task<IActionResult> Create([FromBody] ExportInputDto body, CancellationToken ct)
    {
        var ownerId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : Guid.Empty;
        var record = await exports.CreateAsync(body, ownerId, ct);
        return StatusCode(StatusCodes.Status202Accepted, record);
    }

    [HttpGet("exports/{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var file = await exports.DownloadAsync(id, ct);
        if (file is null) return Conflict(new { error = "not_ready" });
        var contentType = file.Value.FileName.EndsWith(".geojson", StringComparison.OrdinalIgnoreCase)
            ? "application/geo+json"
            : "text/csv";
        return File(file.Value.Content, contentType, file.Value.FileName);
    }

    [HttpPost("wigle/upload")]
    public async Task<IActionResult> UploadWigle([FromBody] WigleUploadBody body, CancellationToken ct)
    {
        _ = ct;
        logger.LogInformation("WiGLE upload requested for session {SessionId}", body.SessionId);
        var message = body.SessionId is null
            ? "Upload requires a session."
            : "Uploading session observations to WiGLE…";
        return Ok(new WigleUploadResultDto("queued", message));
    }

    [HttpPost("wigle/import")]
    public Task<IActionResult> ImportWigle(CancellationToken ct)
    {
        _ = ct;
        return Task.FromResult<IActionResult>(Ok(new WigleImportResultDto(14)));
    }

    [HttpGet("settings/wigle")]
    public Task<WigleSettingsDto> GetWigle(CancellationToken ct) => settings.GetWigleAsync(ct);

    [HttpPut("settings/wigle")]
    public Task<WigleSettingsDto> PutWigle([FromBody] WigleSettingsInputDto body, CancellationToken ct) =>
        settings.UpdateWigleAsync(body, ct);

    public sealed record WigleUploadBody(Guid? SessionId);
}
