using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Einsparungs.Api.Security;
using Microsoft.Extensions.Configuration;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class OfflineLicenseValidatorTests
{
    [TestMethod]
    public void Validate_WithoutInstalledKey_ReturnsNotConfigured()
    {
        var validator = CreateValidator();

        var result = validator.Validate(null);

        Assert.AreEqual(LicenseStatuses.NotConfigured, result.Status);
    }

    [TestMethod]
    public void Validate_WithValidSignatureAndDates_ReturnsActive()
    {
        using var rsa = RSA.Create(2048);
        var now = DateTime.UtcNow.Date;
        var validator = CreateValidator(rsa);
        var key = CreateLicense(rsa, now.AddDays(-1), now.AddDays(30));

        var result = validator.Validate(key, now);

        Assert.AreEqual(LicenseStatuses.Active, result.Status);
        Assert.AreEqual("LIC-TEST-001", result.Payload?.LicenseId);
        Assert.IsTrue(result.IsValidForOperation);
    }

    [TestMethod]
    public void Validate_AfterValidityBeforeGrace_ReturnsGracePeriod()
    {
        using var rsa = RSA.Create(2048);
        var now = DateTime.UtcNow.Date;
        var validator = CreateValidator(rsa);
        var key = CreateLicense(rsa, now.AddDays(-31), now.AddDays(-1), gracePeriodDays: 30);

        var result = validator.Validate(key, now);

        Assert.AreEqual(LicenseStatuses.GracePeriod, result.Status);
        Assert.IsTrue(result.IsValidForOperation);
    }

    [TestMethod]
    public void Validate_AfterGrace_ReturnsExpired()
    {
        using var rsa = RSA.Create(2048);
        var now = DateTime.UtcNow.Date;
        var validator = CreateValidator(rsa);
        var key = CreateLicense(rsa, now.AddDays(-70), now.AddDays(-31), gracePeriodDays: 30);

        var result = validator.Validate(key, now);

        Assert.AreEqual(LicenseStatuses.Expired, result.Status);
        Assert.IsFalse(result.IsValidForOperation);
    }

    [TestMethod]
    public void Validate_WhenSignatureIsTampered_ReturnsInvalid()
    {
        using var rsa = RSA.Create(2048);
        var now = DateTime.UtcNow.Date;
        var validator = CreateValidator(rsa);
        var key = CreateLicense(rsa, now.AddDays(-1), now.AddDays(30));
        var segments = key.Split('.');
        var signature = segments[2];
        var tamperedSignature = (signature[0] == 'A' ? 'B' : 'A') + signature[1..];
        var tampered = $"{segments[0]}.{segments[1]}.{tamperedSignature}";

        var result = validator.Validate(tampered, now);

        Assert.AreEqual(LicenseStatuses.Invalid, result.Status);
    }

    [TestMethod]
    public void Validate_WhenInstallationIdDoesNotMatch_ReturnsInvalid()
    {
        using var rsa = RSA.Create(2048);
        var now = DateTime.UtcNow.Date;
        var validator = CreateValidator(rsa, "installation-local");
        var key = CreateLicense(rsa, now.AddDays(-1), now.AddDays(30), installationId: "installation-other");

        var result = validator.Validate(key, now);

        Assert.AreEqual(LicenseStatuses.Invalid, result.Status);
    }

    [TestMethod]
    public void Validate_WithNonPositiveMaxUsers_ReturnsInvalid()
    {
        using var rsa = RSA.Create(2048);
        var now = DateTime.UtcNow.Date;
        var validator = CreateValidator(rsa);
        var key = CreateLicense(rsa, now.AddDays(-1), now.AddDays(30), maxUsers: 0);

        var result = validator.Validate(key, now);

        Assert.AreEqual(LicenseStatuses.Invalid, result.Status);
    }

    private static OfflineLicenseValidator CreateValidator(RSA? rsa = null, string? installationId = null)
    {
        var values = new Dictionary<string, string?>();
        if (rsa is not null)
        {
            values["License:PublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem();
        }

        if (installationId is not null)
        {
            values["License:InstallationId"] = installationId;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new OfflineLicenseValidator(configuration);
    }

    private static string CreateLicense(
        RSA rsa,
        DateTime validFrom,
        DateTime validUntil,
        int gracePeriodDays = 30,
        string? installationId = "installation-local",
        int? maxUsers = 25)
    {
        var payload = new
        {
            licenseId = "LIC-TEST-001",
            customerName = "Testkunde",
            product = "HimiFlow",
            validFrom,
            validUntil,
            gracePeriodDays,
            maxUsers,
            features = new[] { "core" },
            installationId
        };

        var payloadSegment = ToBase64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(payloadSegment),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"HIMIFLOW-LICENSE-V1.{payloadSegment}.{ToBase64Url(signature)}";
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
