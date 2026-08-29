using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.DTOs;

public sealed record LicenseInstallRequest(
    [param: Required] string LicenseKey);

public sealed record LicenseStatusResponse(
    string Status,
    string? LicenseId,
    string? CustomerName,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    DateTime? GraceUntil,
    int? DaysRemaining,
    bool IsReadOnly,
    int? MaxUsers,
    IReadOnlyList<string> Features,
    string? InstallationId,
    DateTime? InstalledAt,
    string? Message);
