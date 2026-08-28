using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.DTOs;

public sealed record LicenseInstallRequest(
    [property: Required] string LicenseKey);

public sealed record LicenseStatusResponse(
    string Status,
    string? LicenseId,
    string? CustomerName,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    DateTime? GraceUntil,
    int? DaysRemaining,
    bool IsReadOnly,
    string? InstallationId,
    DateTime? InstalledAt,
    string? Message);
