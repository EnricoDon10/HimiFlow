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

    public LicenseService(
        AppDbContext db,
        OfflineLicenseValidator validator,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        this.db = db;
        this.validator = validator;
        this.configuration = configuration;
        this.environment = environment;
    }

    public bool IsEnforcementEnabled =>
        configuration.GetValue<bool?>("License:EnforcementEnabled") ?? !environment.IsDevelopment();

    public async Task<LicenseStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var installation = await db.LicenseInstallations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);

        var validation = validator.Validate(installation?.LicenseKey);
        return ToResponse(validation, installation);
    }

    public async Task<(bool Succeeded, LicenseStatusResponse Status, string? Error)> InstallAsync(
        string licenseKey,
        Guid installedByUserId,
        CancellationToken cancellationToken = default)
    {
        var validation = validator.Validate(licenseKey);

        if (!validation.IsValidForOperation || validation.Payload is null)
        {
            return (false, ToResponse(validation, null), validation.Error ?? "Die Lizenz ist nicht gültig.");
        }

        var installation = await db.LicenseInstallations
            .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);

        if (installation is null)
        {
            installation = new LicenseInstallation { Id = 1 };
            db.LicenseInstallations.Add(installation);
        }

        installation.LicenseKey = licenseKey.Trim();
        installation.InstalledAt = DateTime.UtcNow;
        installation.InstalledByUserId = installedByUserId;
        await db.SaveChangesAsync(cancellationToken);

        return (true, ToResponse(validation, installation), null);
    }

    public async Task<LicenseValidationResult> ValidateCurrentAsync(CancellationToken cancellationToken = default)
    {
        var key = await db.LicenseInstallations
            .AsNoTracking()
            .Where(item => item.Id == 1)
            .Select(item => item.LicenseKey)
            .SingleOrDefaultAsync(cancellationToken);

        return validator.Validate(key);
    }

    private LicenseStatusResponse ToResponse(
        LicenseValidationResult validation,
        LicenseInstallation? installation)
    {
        var payload = validation.Payload;
        var referenceDate = DateTime.UtcNow;
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
            payload?.InstallationId,
            installation?.InstalledAt,
            validation.Error ?? StatusMessage(validation.Status, daysRemaining));
    }

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
