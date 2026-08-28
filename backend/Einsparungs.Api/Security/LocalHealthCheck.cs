using Einsparungs.Api.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Einsparungs.Api.Security;

public sealed class LocalHealthCheck : IHealthCheck
{
    private readonly AppDbContext db;
    private readonly IConfiguration configuration;

    public LocalHealthCheck(AppDbContext db, IConfiguration configuration)
    {
        this.db = db;
        this.configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            var provider = configuration["Database:Provider"] ?? "SQLite";
            return canConnect
                ? HealthCheckResult.Healthy($"{provider} erreichbar.")
                : HealthCheckResult.Unhealthy($"{provider} nicht erreichbar.");
        }
        catch (Exception exception)
        {
            var provider = configuration["Database:Provider"] ?? "SQLite";
            return HealthCheckResult.Unhealthy($"{provider}-Prüfung fehlgeschlagen.", exception);
        }
    }
}
