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
    private readonly BackupStatusEvaluator backupStatusEvaluator;

    public OperationsController(
        SqliteBackupService backupService,
        AppDbContext db,
        BackupStatusEvaluator backupStatusEvaluator)
    {
        this.backupService = backupService;
        this.db = db;
        this.backupStatusEvaluator = backupStatusEvaluator;
    }

    [HttpGet("backup-status")]
    public ActionResult<BackupStatusResponse> GetBackupStatus()
    {
        try
        {
            return Ok(backupStatusEvaluator.Evaluate());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Backup-Status nicht verfügbar",
                Detail = $"Das Backup-Verzeichnis konnte nicht gelesen werden: {exception.Message}",
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return StatusCode(StatusCodes.Status503ServiceUnavailable, problem);
        }
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

        return Ok(new BackupResponse(
            backup.FileName,
            backup.SizeBytes,
            backup.CreatedAtUtc,
            "Gültig",
            DateTime.UtcNow));
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Der aktuelle Benutzer konnte nicht ermittelt werden.");
    }
}
