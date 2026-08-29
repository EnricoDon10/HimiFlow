namespace Einsparungs.Api.Security;

public sealed class AuditRetentionOptions
{
    public bool CleanupEnabled { get; set; }
    public int RetentionDays { get; set; }
    public int CheckIntervalHours { get; set; } = 24;
    public int BatchSize { get; set; } = 500;
}
