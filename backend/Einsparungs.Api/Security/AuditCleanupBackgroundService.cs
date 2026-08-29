using Microsoft.Extensions.Options;

namespace Einsparungs.Api.Security;

public sealed class AuditCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IOptions<AuditRetentionOptions> options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<AuditCleanupBackgroundService> logger;

    public AuditCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<AuditRetentionOptions> options,
        TimeProvider timeProvider,
        ILogger<AuditCleanupBackgroundService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.options = options;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configured = options.Value;
        if (!configured.CleanupEnabled || configured.RetentionDays <= 0)
        {
            logger.LogInformation(
                "Automatische Audit-Bereinigung ist deaktiviert. AuditLogs werden nicht automatisch gelöscht.");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Clamp(configured.CheckIntervalHours, 1, 168));
        using var timer = new PeriodicTimer(interval, timeProvider);

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<AuditRetentionService>();
                await service.CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Automatische Audit-Bereinigung ist fehlgeschlagen.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
