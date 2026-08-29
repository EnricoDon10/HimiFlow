using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Security;

public sealed class LicenseService
{
    private readonly AppDbContext db;
    private readonly OfflineLicenseValidator validator;
    private readonly IConfiguration configuration;
    private readonly IHostEnvironment environment;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<LicenseService> logger;

    public LicenseService(
        AppDbContext db,
        OfflineLicenseValidator validator,
        IConfiguration configuration,
        IHostEnvironment environment,
        TimeProvider timeProvider,
        ILogger<LicenseService> logger)
    {
        this.db = db;
        this.validator = validator;
        this.configuration = configuration;
        this.environment = environment;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public bool IsEnforcementEnabled =>
        configuration.GetValue<bool?>("License:EnforcementEnabled") ?? !environment.IsDevelopment();

    public async Task<LicenseStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var installation = await db.LicenseInstallations
            .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);

        var validation = await ValidateInstallationAsync(installation, cancellationToken);
        return ToResponse(validation, installation);
    }

    public async Task<(bool Succeeded, LicenseStatusResponse Status, string? Error)> InstallAsync(
        string licenseKey,
        Guid installedByUserId,
        CancellationToken cancellationToken = default)
    {
        var now = UtcNow();
        var validation = validator.Validate(licenseKey, now);

        if (!validation.IsValidForOperation || validation.Payload is null)
        {
            return (false, ToResponse(validation, null), validation.Error ?? "Die Lizenz ist nicht gültig.");
        }

        var installation = await db.LicenseInstallations
            .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);

        var clockError = DetectClockRollback(installation, now);
        if (clockError is not null)
        {
            return (false, ToResponse(clockError, installation), clockError.Error);
        }

        if (installation is null)
        {
            installation = new LicenseInstallation { Id = 1 };
            db.LicenseInstallations.Add(installation);
        }

        installation.LicenseKey = licenseKey.Trim();
        installation.InstalledAt = now;
        installation.InstalledByUserId = installedByUserId;
        installation.LastSuccessfulLicenseValidationUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        return (true, ToResponse(validation, installation), null);
    }

    public async Task<LicenseValidationResult> ValidateCurrentAsync(CancellationToken cancellationToken = default)
    {
        var installation = await db.LicenseInstallations
            .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);

        return await ValidateInstallationAsync(installation, cancellationToken);
    }

    public async Task<LicenseSeatLimitResult> CheckActiveUserSlotAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsEnforcementEnabled)
        {
            return LicenseSeatLimitResult.Unlimited;
        }

        var validation = await ValidateCurrentAsync(cancellationToken);
        var maxUsers = validation.IsValidForOperation
            ? validation.Payload?.MaxUsers
            : null;

        if (!maxUsers.HasValue)
        {
            return LicenseSeatLimitResult.Unlimited;
        }

        var activeUsers = await db.Users.CountAsync(
            user => user.IsActive && !user.IsDeleted,
            cancellationToken);

        return new LicenseSeatLimitResult(
            activeUsers < maxUsers.Value,
            maxUsers,
            activeUsers);
    }

    private LicenseStatusResponse ToResponse(
        LicenseValidationResult validation,
        LicenseInstallation? installation)
    {
        var payload = validation.Payload;
        var referenceDate = UtcNow();
        var endDate = validation.Status == LicenseStatuses.GracePeriod
            ? validation.GraceUntil
            : payload?.ValidUntil;
        int? daysRemaining = endDate.HasValue
            ? Math.Max(0, (int)Math.Ceiling((endDate.Value - referenceDate).TotalDays))
            : null;

        return new LicenseStatusResponse(
            validation.Status,
            payload?.LicenseId,
            payload?.CustomerName,
            payload?.ValidFrom,
            payload?.ValidUntil,
            validation.GraceUntil,
            daysRemaining,
            IsReadOnly(validation.Status),
            payload?.MaxUsers,
            payload?.Features ?? Array.Empty<string>(),
            payload?.InstallationId,
            installation?.InstalledAt,
            validation.Error ?? StatusMessage(validation.Status, daysRemaining));
    }

    private async Task<LicenseValidationResult> ValidateInstallationAsync(
        LicenseInstallation? installation,
        CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var clockError = DetectClockRollback(installation, now);

        if (clockError is not null)
        {
            return clockError;
        }

        var validation = validator.Validate(installation?.LicenseKey, now);
        if (!validation.IsValidForOperation || installation is null)
        {
            return validation;
        }

        var checkpointInterval = TimeSpan.FromMinutes(Math.Clamp(
            configuration.GetValue("License:ValidationCheckpointIntervalMinutes", 60),
            5,
            1440));
        var checkpoint = installation.LastSuccessfulLicenseValidationUtc;

        if (!checkpoint.HasValue || now - EnsureUtc(checkpoint.Value) >= checkpointInterval)
        {
            installation.LastSuccessfulLicenseValidationUtc = now;
            await db.SaveChangesAsync(cancellationToken);
        }

        return validation;
    }

    private LicenseValidationResult? DetectClockRollback(
        LicenseInstallation? installation,
        DateTime now)
    {
        if (installation?.LastSuccessfulLicenseValidationUtc is null)
        {
            return null;
        }

        var checkpoint = EnsureUtc(installation.LastSuccessfulLicenseValidationUtc.Value);
        var tolerance = TimeSpan.FromMinutes(Math.Clamp(
            configuration.GetValue("License:ClockRollbackToleranceMinutes", 5),
            1,
            60));

        if (now >= checkpoint - tolerance)
        {
            return null;
        }

        logger.LogWarning(
            "Lizenzvalidierung wegen möglicher Zeitrückstellung abgelehnt. CurrentUtc: {CurrentUtc}; LastSuccessfulUtc: {LastSuccessfulUtc}",
            now,
            checkpoint);
        return new LicenseValidationResult(
            LicenseStatuses.Invalid,
            null,
            null,
            "Die Systemzeit liegt deutlich vor der letzten erfolgreichen Lizenzprüfung. Bitte Systemzeit und Zeitsynchronisation prüfen.");
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    private bool IsReadOnly(string status)
    {
        return IsEnforcementEnabled && status is not (LicenseStatuses.Active or LicenseStatuses.GracePeriod);
    }

    private static string? StatusMessage(string status, int? daysRemaining)
    {
        return status switch
        {
            LicenseStatuses.GracePeriod => $"Die Lizenz befindet sich in der Grace-Period ({daysRemaining ?? 0} Tage verbleiben).",
            LicenseStatuses.NotConfigured => "Noch keine Lizenz installiert.",
            _ => null
        };
    }
}

public sealed record LicenseSeatLimitResult(
    bool SlotAvailable,
    int? MaxUsers,
    int ActiveUsers)
{
    public static LicenseSeatLimitResult Unlimited { get; } = new(true, null, 0);
}
