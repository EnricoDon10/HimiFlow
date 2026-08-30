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
        return Ok(backupStatusEvaluator.Evaluate());
    }

    [HttpGet("backups")]
    public ActionResult<IReadOnlyList<BackupResponse>> ListBackups()
    {
        return Ok(backupService.List().Select(backup => ToResponse(backup)).ToArray());
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

        return Ok(ToResponse(backup, "Gültig", DateTime.UtcNow));
    }

    [HttpPost("backups/{fileName}/validate")]
    public async Task<ActionResult<BackupValidationResponse>> ValidateBackup(
        string fileName,
        CancellationToken cancellationToken)
    {
        var validation = await backupService.ValidateNamedAsync(fileName, cancellationToken);
        var checkedAt = DateTime.UtcNow;
        await WriteRecoveryAuditAsync(
            validation.IsValid ? "BackupValidated" : "BackupValidationFailed",
            fileName,
            new { validation.IsValid, validation.Result, CheckedAtUtc = checkedAt },
            cancellationToken);

        return Ok(new BackupValidationResponse(fileName, validation.IsValid, validation.Result, checkedAt));
    }

    [HttpPost("backups/{fileName}/prepare-restore")]
    public async Task<ActionResult<RestorePreparationResponse>> PrepareRestore(
        string fileName,
        CancellationToken cancellationToken)
    {
        var validation = await backupService.ValidateNamedAsync(fileName, cancellationToken);
        var checkedAt = DateTime.UtcNow;
        var message = validation.IsValid
            ? "Backup ist gültig. Die Wiederherstellung erfolgt ausschließlich im Wartungsmodus; HimiFlow muss dafür vollständig beendet werden."
            : "Das ausgewählte Backup ist ungültig und darf nicht wiederhergestellt werden.";
        await WriteRecoveryAuditAsync(
            validation.IsValid ? "RestorePrepared" : "RestorePreparationRejected",
            fileName,
            new { validation.IsValid, validation.Result, CheckedAtUtc = checkedAt },
            cancellationToken);

        return Ok(new RestorePreparationResponse(fileName, validation.IsValid, message, checkedAt));
    }

    private static BackupResponse ToResponse(
        SqliteBackupService.BackupFile backup,
        string integrityStatus = "Nicht geprüft",
        DateTime? lastValidatedAtUtc = null)
    {
        return new BackupResponse(
            backup.FileName,
            backup.SizeBytes,
            backup.CreatedAtUtc,
            integrityStatus,
            lastValidatedAtUtc);
    }

    private async Task WriteRecoveryAuditAsync(
        string action,
        string fileName,
        object values,
        CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(new AuditLog
        {
            EntityName = "Database",
            EntityId = "sqlite",
            Action = action,
            ChangedByUserId = GetCurrentUserId(),
            ChangedAt = DateTime.UtcNow,
            NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new { fileName, values }),
            ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Der aktuelle Benutzer konnte nicht ermittelt werden.");
    }
}
