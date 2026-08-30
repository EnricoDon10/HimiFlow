namespace Einsparungs.Api.DTOs;

public sealed record BackupResponse(
    string FileName,
    long SizeBytes,
    DateTime CreatedAtUtc,
    string IntegrityStatus,
    DateTime? LastValidatedAtUtc);

public sealed record BackupValidationResponse(
    string FileName,
    bool IsValid,
    string Result,
    DateTime CheckedAtUtc);

public sealed record RestorePreparationResponse(
    string FileName,
    bool IsValid,
    string Message,
    DateTime CheckedAtUtc);

public sealed record BackupStatusResponse(
    bool AutomaticEnabled,
    int IntervalHours,
    int MaximumAgeHours,
    int RetentionDays,
    int MinimumBackupsToKeep,
    DateTime? LatestBackupAtUtc,
    DateTime? NextBackupDueAtUtc,
    int AvailableBackups,
    bool IsMissing,
    bool IsOverdue,
    string Status,
    string Message);
