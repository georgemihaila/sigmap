using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigmap.Application.Dtos;
using Sigmap.Application.Services;

namespace Sigmap.Api.Controllers;

[ApiController]
[Authorize]
public class FleetController(IFleetService fleet) : ControllerBase
{
    [HttpGet("api/fleet")]
    public Task<List<FleetDeviceDto>> List(CancellationToken ct) => fleet.ListAsync(null, ct);
}
