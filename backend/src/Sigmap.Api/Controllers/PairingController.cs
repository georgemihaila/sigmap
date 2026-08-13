using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigmap.Application.Dtos;
using Sigmap.Application.Services;

namespace Sigmap.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/pairing")]
public class PairingController(IPairingService pairing) : ControllerBase
{
    [HttpGet("pending")]
    public Task<List<PairingRequestDto>> Pending(CancellationToken ct) => pairing.ListPendingAsync(ct);

    [HttpPost("{deviceId:guid}/approve")]
    [Authorize(Roles = "operator")]
    public async Task<IActionResult> Approve(Guid deviceId, [FromBody] ApprovePairingRequest? body, CancellationToken ct)
    {
        var ok = await pairing.ApproveAsync(deviceId, body?.SessionId, ct);
        return ok ? NoContent() : NotFound(new { error = "not_found" });
    }

    [HttpPost("{deviceId:guid}/reject")]
    [Authorize(Roles = "operator")]
    public async Task<IActionResult> Reject(Guid deviceId, CancellationToken ct)
    {
        var ok = await pairing.RejectAsync(deviceId, ct);
        return ok ? NoContent() : NotFound(new { error = "not_found" });
    }

    [HttpGet("qr")]
    public async Task<IActionResult> Qr(CancellationToken ct)
    {
        var qr = await pairing.GetQrAsync(ct);
        var backendHost = $"{Request.Scheme}://{Request.Host}/api";
        return Ok(new PairingQrDto(qr.Token, qr.Payload.Replace("__BACKEND_HOST__", backendHost)));
    }
}
