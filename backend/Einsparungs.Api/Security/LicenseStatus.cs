namespace Einsparungs.Api.Security;

public static class LicenseStatuses
{
    public const string Active = "ACTIVE";
    public const string GracePeriod = "GRACE_PERIOD";
    public const string Expired = "EXPIRED";
    public const string Invalid = "INVALID";
    public const string NotConfigured = "NOT_CONFIGURED";
}

public sealed record LicenseValidationResult(
    string Status,
    LicenseTokenPayload? Payload,
    DateTime? GraceUntil,
    string? Error)
{
    public bool IsValidForOperation =>
        Status is LicenseStatuses.Active or LicenseStatuses.GracePeriod;
}

public sealed record LicenseTokenPayload(
    string LicenseId,
    string CustomerName,
    string Product,
    DateTime ValidFrom,
    DateTime ValidUntil,
    DateTime? GraceUntil,
    int? MaxUsers,
    string[]? Features,
    string? InstallationId);
