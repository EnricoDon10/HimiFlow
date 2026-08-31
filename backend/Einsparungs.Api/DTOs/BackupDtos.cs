namespace Einsparungs.Api.DTOs;

public sealed record BackupResponse(
    string FileName,
    long SizeBytes,
    DateTime CreatedAtUtc,
    string IntegrityStatus,
    DateTime? LastValidatedAtUtc);

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
