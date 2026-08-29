using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Einsparungs.Api.Security;

public sealed class BackupOperationsHealthCheck(BackupStatusEvaluator evaluator) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var status = evaluator.Evaluate();
        var data = new Dictionary<string, object>
        {
            ["status"] = status.Status,
            ["availableBackups"] = status.AvailableBackups,
            ["maximumAgeHours"] = status.MaximumAgeHours,
            ["latestBackupAtUtc"] = status.LatestBackupAtUtc?.ToString("O") ?? string.Empty
        };

        return Task.FromResult(status.Status switch
        {
            "CURRENT" or "EXTERNAL_PROVIDER" => HealthCheckResult.Healthy(status.Message, data),
            _ => HealthCheckResult.Degraded(status.Message, data: data)
        });
    }
}
