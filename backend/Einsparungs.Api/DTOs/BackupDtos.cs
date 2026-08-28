namespace Einsparungs.Api.DTOs;

public sealed record BackupResponse(
    string FileName,
    long SizeBytes,
    DateTime CreatedAtUtc,
    string RelativePath);

public sealed record BackupStatusResponse(
    bool AutomaticEnabled,
    int IntervalHours,
    int RetentionDays,
    int MinimumBackupsToKeep,
    DateTime? LatestBackupAtUtc,
    DateTime? NextBackupDueAtUtc,
    int AvailableBackups);
