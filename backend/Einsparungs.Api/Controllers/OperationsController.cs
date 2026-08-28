using System.Security.Claims;
using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/operations")]
[Authorize(Roles = ApplicationRoles.SystemAdmin)]
public sealed class OperationsController : ControllerBase
{
    private readonly SqliteBackupService backupService;
    private readonly AppDbContext db;

    public OperationsController(SqliteBackupService backupService, AppDbContext db)
    {
        this.backupService = backupService;
        this.db = db;
    }

    [HttpGet("backups")]
    public ActionResult<IReadOnlyList<BackupResponse>> ListBackups()
    {
        return Ok(backupService.List().Select(ToResponse).ToArray());
    }

    [HttpPost("backups")]
    public async Task<ActionResult<BackupResponse>> CreateBackup(CancellationToken cancellationToken)
    {
        var backup = await backupService.CreateAsync(cancellationToken);
        var userId = GetCurrentUserId();

        db.AuditLogs.Add(new AuditLog
        {
            EntityName = "Database",
            EntityId = "sqlite",
            Action = "BackupCreated",
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow,
            NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                backup.FileName,
                backup.SizeBytes,
                backup.CreatedAtUtc
            }),
            ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        });
        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(backup));
    }

    private static BackupResponse ToResponse(SqliteBackupService.BackupFile backup)
    {
        return new BackupResponse(
            backup.FileName,
            backup.SizeBytes,
            backup.CreatedAtUtc,
            Path.Combine("backups", backup.FileName).Replace('\\', '/'));
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Der aktuelle Benutzer konnte nicht ermittelt werden.");
    }
}
