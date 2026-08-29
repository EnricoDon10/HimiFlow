using Einsparungs.Api.DTOs;

namespace Einsparungs.Api.Security;

public sealed class BackupStatusEvaluator
{
    private readonly SqliteBackupService backupService;
    private readonly IConfiguration configuration;
    private readonly TimeProvider timeProvider;

    public BackupStatusEvaluator(
        SqliteBackupService backupService,
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        this.backupService = backupService;
        this.configuration = configuration;
        this.timeProvider = timeProvider;
    }

    public BackupStatusResponse Evaluate()
    {
        var intervalHours = Math.Clamp(configuration.GetValue("Backup:IntervalHours", 24), 1, 168);
        var maximumAgeHours = Math.Clamp(
            configuration.GetValue("Backup:MaximumAgeHours", Math.Max(36, intervalHours)),
            intervalHours,
            720);
        var backups = backupService.List();
        var latest = backups.FirstOrDefault();
        var automaticEnabled = backupService.IsSqliteProvider &&
                               configuration.GetValue("Backup:AutomaticEnabled", true);
        var isMissing = backupService.IsSqliteProvider && latest is null;
        var isOverdue = latest is not null &&
                        latest.CreatedAtUtc.AddHours(maximumAgeHours) < timeProvider.GetUtcNow().UtcDateTime;
        var (status, message) = GetStatus(
            automaticEnabled,
            backupService.IsSqliteProvider,
            isMissing,
            isOverdue);

        return new BackupStatusResponse(
            automaticEnabled,
            intervalHours,
            maximumAgeHours,
            configuration.GetValue("Backup:RetentionDays", 30),
            configuration.GetValue("Backup:MinimumBackupsToKeep", 7),
            latest?.CreatedAtUtc,
            automaticEnabled
                ? latest?.CreatedAtUtc.AddHours(intervalHours) ?? timeProvider.GetUtcNow().UtcDateTime
                : null,
            backups.Count,
            isMissing,
            isOverdue,
            status,
            message);
    }

    private static (string Status, string Message) GetStatus(
        bool automaticEnabled,
        bool isSqliteProvider,
        bool isMissing,
        bool isOverdue)
    {
        if (!isSqliteProvider)
        {
            return ("EXTERNAL_PROVIDER", "Backups werden für diesen Datenbankprovider außerhalb von HimiFlow betrieben.");
        }

        if (!automaticEnabled)
        {
            return ("DISABLED", "Automatische SQLite-Backups sind deaktiviert.");
        }

        if (isMissing)
        {
            return ("MISSING", "Es wurde noch kein lesbares SQLite-Backup gefunden.");
        }

        if (isOverdue)
        {
            return ("OVERDUE", "Das letzte SQLite-Backup ist älter als das zulässige Maximalalter.");
        }

        return ("CURRENT", "Das letzte SQLite-Backup liegt innerhalb des zulässigen Maximalalters.");
    }
}
