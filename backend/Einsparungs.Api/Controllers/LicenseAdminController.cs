using System.Security.Claims;
using System.Data;
using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/admin/license")]
[Authorize(Roles = ApplicationRoles.SystemAdmin)]
public sealed class LicenseAdminController : ControllerBase
{
    private readonly LicenseService licenseService;
    private readonly AppDbContext db;

    public LicenseAdminController(LicenseService licenseService, AppDbContext db)
    {
        this.licenseService = licenseService;
        this.db = db;
    }

    [HttpPost]
    public async Task<ActionResult<LicenseStatusResponse>> Install(
        [FromBody] LicenseInstallRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userId, out var installedByUserId))
        {
            return Unauthorized();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var result = await licenseService.InstallAsync(request.LicenseKey, installedByUserId, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                [result.Error ?? "Die Lizenz konnte nicht installiert werden."]));
        }

        db.AuditLogs.Add(new AuditLog
        {
            EntityName = "License",
            EntityId = "1",
            Action = "Installed",
            ChangedByUserId = installedByUserId,
            ChangedAt = DateTime.UtcNow,
            NewValuesJson = JsonSerializer.Serialize(new
            {
                result.Status.LicenseId,
                result.Status.CustomerName,
                result.Status.ValidFrom,
                result.Status.ValidUntil,
                result.Status.GraceUntil,
                result.Status.InstallationId
            }),
            ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(result.Status);
    }
}
