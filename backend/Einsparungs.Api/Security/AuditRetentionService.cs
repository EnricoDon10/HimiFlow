using Einsparungs.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Einsparungs.Api.Security;

public sealed class AuditRetentionService
{
    private readonly AppDbContext db;
    private readonly AuditRetentionOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<AuditRetentionService> logger;

    public AuditRetentionService(
        AppDbContext db,
        IOptions<AuditRetentionOptions> options,
        TimeProvider timeProvider,
        ILogger<AuditRetentionService> logger)
    {
        this.db = db;
        this.options = options.Value;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task<int> CleanupAsync(CancellationToken cancellationToken = default)
    {
        if (!options.CleanupEnabled || options.RetentionDays <= 0)
        {
            return 0;
        }

        var cutoffUtc = timeProvider.GetUtcNow().UtcDateTime.AddDays(-options.RetentionDays);
        var batchSize = Math.Clamp(options.BatchSize, 1, 5_000);
        var deletedTotal = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var ids = await db.AuditLogs
                .AsNoTracking()
                .Where(log => log.ChangedAt < cutoffUtc)
                .OrderBy(log => log.ChangedAt)
                .ThenBy(log => log.Id)
                .Select(log => log.Id)
                .Take(batchSize)
                .ToArrayAsync(cancellationToken);

            if (ids.Length == 0)
            {
                break;
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var deleted = await db.AuditLogs
                .Where(log => ids.Contains(log.Id))
                .ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            deletedTotal += deleted;

            if (ids.Length < batchSize)
            {
                break;
            }
        }

        if (deletedTotal > 0)
        {
            logger.LogWarning(
                "Audit-Aufbewahrung hat {DeletedCount} Protokolleinträge vor {CutoffUtc} gelöscht.",
                deletedTotal,
                cutoffUtc);
        }

        return deletedTotal;
    }
}
