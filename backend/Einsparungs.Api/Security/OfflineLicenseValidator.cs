using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Einsparungs.Api.Security;

public sealed class OfflineLicenseValidator
{
    private const string Prefix = "HIMIFLOW-LICENSE-V1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IConfiguration configuration;

    public OfflineLicenseValidator(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public LicenseValidationResult Validate(string? licenseKey, DateTime? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return new LicenseValidationResult(
                LicenseStatuses.NotConfigured,
                null,
                null,
                "Es ist noch keine Lizenz installiert.");
        }

        var publicKeyPem = configuration["License:PublicKeyPem"];

        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            return Invalid("Der Lizenz-Public-Key ist nicht konfiguriert.");
        }

        try
        {
            var segments = licenseKey.Trim().Split('.', StringSplitOptions.None);

            if (segments.Length != 3 || !string.Equals(segments[0], Prefix, StringComparison.Ordinal))
            {
                return Invalid("Das Lizenzformat ist ungültig.");
            }

            var payloadBytes = DecodeBase64Url(segments[1]);
            var signatureBytes = DecodeBase64Url(segments[2]);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);

            var signatureIsValid = rsa.VerifyData(
                Encoding.UTF8.GetBytes(segments[1]),
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            if (!signatureIsValid)
            {
                return Invalid("Die Lizenzsignatur ist ungültig.");
            }

            var wirePayload = JsonSerializer.Deserialize<LicenseTokenWirePayload>(payloadBytes, JsonOptions);

            if (wirePayload is null ||
                string.IsNullOrWhiteSpace(wirePayload.LicenseId) ||
                string.IsNullOrWhiteSpace(wirePayload.CustomerName) ||
                !string.Equals(wirePayload.Product, "HimiFlow", StringComparison.OrdinalIgnoreCase) ||
                wirePayload.ValidUntil <= wirePayload.ValidFrom)
            {
                return Invalid("Die Lizenzdaten sind unvollständig oder gehören zu einem anderen Produkt.");
            }

            var configuredInstallationId = configuration["License:InstallationId"]?.Trim();

            if (!string.IsNullOrWhiteSpace(configuredInstallationId) &&
                !string.Equals(configuredInstallationId, wirePayload.InstallationId, StringComparison.Ordinal))
            {
                return Invalid("Die Lizenz gehört nicht zu dieser Installations-ID.");
            }

            var graceUntil = wirePayload.GraceUntil ??
                wirePayload.ValidUntil.AddDays(Math.Clamp(wirePayload.GracePeriodDays ?? 30, 0, 30));

            if (graceUntil < wirePayload.ValidUntil ||
                graceUntil > wirePayload.ValidUntil.AddDays(30))
            {
                return Invalid("Die Grace-Period der Lizenz ist ungültig.");
            }

            var payload = new LicenseTokenPayload(
                wirePayload.LicenseId,
                wirePayload.CustomerName,
                wirePayload.Product,
                EnsureUtc(wirePayload.ValidFrom),
                EnsureUtc(wirePayload.ValidUntil),
                wirePayload.GraceUntil is null ? null : EnsureUtc(wirePayload.GraceUntil.Value),
                wirePayload.MaxUsers,
                wirePayload.Features,
                wirePayload.InstallationId);

            var now = EnsureUtc(nowUtc ?? DateTime.UtcNow);
            var validFrom = payload.ValidFrom;
            var validUntil = payload.ValidUntil;
            var effectiveGraceUntil = payload.GraceUntil ?? validUntil.AddDays(Math.Clamp(wirePayload.GracePeriodDays ?? 30, 0, 30));

            if (now < validFrom)
            {
                return Invalid("Die Lizenz ist noch nicht gültig.");
            }

            if (now <= validUntil)
            {
                return new LicenseValidationResult(LicenseStatuses.Active, payload, effectiveGraceUntil, null);
            }

            if (now <= effectiveGraceUntil)
            {
                return new LicenseValidationResult(LicenseStatuses.GracePeriod, payload, effectiveGraceUntil, null);
            }

            return new LicenseValidationResult(
                LicenseStatuses.Expired,
                payload,
                effectiveGraceUntil,
                "Die Lizenz ist abgelaufen. HimiFlow läuft im schreibgeschützten Modus.");
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or JsonException or ArgumentException)
        {
            return Invalid("Die Lizenz konnte nicht validiert werden.");
        }
    }

    private static LicenseValidationResult Invalid(string error)
    {
        return new LicenseValidationResult(LicenseStatuses.Invalid, null, null, error);
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += (normalized.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty
        };

        return Convert.FromBase64String(normalized);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    private sealed record LicenseTokenWirePayload(
        [property: JsonPropertyName("licenseId")] string LicenseId,
        [property: JsonPropertyName("customerName")] string CustomerName,
        [property: JsonPropertyName("product")] string Product,
        [property: JsonPropertyName("validFrom")] DateTime ValidFrom,
        [property: JsonPropertyName("validUntil")] DateTime ValidUntil,
        [property: JsonPropertyName("graceUntil")] DateTime? GraceUntil,
        [property: JsonPropertyName("gracePeriodDays")] int? GracePeriodDays,
        [property: JsonPropertyName("maxUsers")] int? MaxUsers,
        [property: JsonPropertyName("features")] string[]? Features,
        [property: JsonPropertyName("installationId")] string? InstallationId);
}
