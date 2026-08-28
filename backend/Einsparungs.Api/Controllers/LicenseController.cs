using Einsparungs.Api.DTOs;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/license")]
[Authorize]
public sealed class LicenseController : ControllerBase
{
    private readonly LicenseService licenseService;

    public LicenseController(LicenseService licenseService)
    {
        this.licenseService = licenseService;
    }

    [HttpGet("status")]
    public async Task<ActionResult<LicenseStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await licenseService.GetStatusAsync(cancellationToken));
    }
}
