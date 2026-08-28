namespace Einsparungs.Api.Security;

public sealed class SqliteBackupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IConfiguration configuration;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<SqliteBackupBackgroundService> logger;

    public SqliteBackupBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        TimeProvider timeProvider,
        ILogger<SqliteBackupBackgroundService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.configuration = configuration;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Backup:AutomaticEnabled", true))
        {
            logger.LogInformation("Automatische SQLite-Backups sind deaktiviert.");
            return;
        }

        if (!string.Equals(
                configuration["Database:Provider"] ?? "SQLite",
                "SQLite",
                StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Automatische SQLite-Backups sind für den aktiven Datenbankprovider nicht relevant.");
            return;
        }

        await RunBackupCycleAsync(stoppingToken);

        var checkMinutes = Math.Clamp(
            configuration.GetValue("Backup:CheckIntervalMinutes", 15),
            5,
            1440);
        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(checkMinutes),
            timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunBackupCycleAsync(stoppingToken);
        }
    }

    private async Task RunBackupCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<SqliteBackupService>();
            var intervalHours = Math.Clamp(
                configuration.GetValue("Backup:IntervalHours", 24),
                1,
                168);
            var latest = backupService.List().FirstOrDefault();
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var isDue = latest is null || latest.CreatedAtUtc.AddHours(intervalHours) <= now;

            if (isDue)
            {
                await backupService.CreateAsync(cancellationToken);
            }

            backupService.PruneExpired(
                configuration.GetValue("Backup:RetentionDays", 30),
                configuration.GetValue("Backup:MinimumBackupsToKeep", 7));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Der automatische SQLite-Backup-Lauf ist fehlgeschlagen.");
        }
    }
}
