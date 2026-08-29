using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class LicenseHttpIntegrationTests
{
    private const string InstallationId = "integration-installation";
    private const string EmployeePassword = "L7@tV2!qR9#kM4$p";

    [TestMethod]
    [DataRow(LicenseStatuses.Active, true)]
    [DataRow(LicenseStatuses.GracePeriod, true)]
    [DataRow(LicenseStatuses.Expired, false)]
    [DataRow(LicenseStatuses.Invalid, false)]
    public async Task LicenseStatusControlsRealHttpWrites(string expectedStatus, bool writeAllowed)
    {
        using var rsa = RSA.Create(2048);
        var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
        using var factory = new HimiFlowWebApplicationFactory(new Dictionary<string, string?>
        {
            ["License:EnforcementEnabled"] = "true",
            ["License:InstallationId"] = InstallationId,
            ["License:PublicKeyPem"] = publicKey
        });
        var employee = await factory.AddUserAsync(
            $"license.{expectedStatus.ToLowerInvariant()}",
            EmployeePassword,
            ApplicationRoles.Mitarbeiter);
        var now = DateTime.UtcNow;
        var licenseKey = expectedStatus switch
        {
            LicenseStatuses.Active => CreateLicense(rsa, now.AddDays(-1), now.AddDays(30), now.AddDays(60)),
            LicenseStatuses.GracePeriod => CreateLicense(rsa, now.AddDays(-400), now.AddDays(-1), now.AddDays(20)),
            LicenseStatuses.Expired => CreateLicense(rsa, now.AddDays(-400), now.AddDays(-60), now.AddDays(-30)),
            _ => "HIMIFLOW-LICENSE-V1.invalid.invalid"
        };

        if (expectedStatus is LicenseStatuses.Active or LicenseStatuses.GracePeriod)
        {
            var adminSession = CreateSession(factory);
            using var adminLogin = await adminSession.LoginAsync(
                HimiFlowWebApplicationFactory.InitialAdminUserName,
                HimiFlowWebApplicationFactory.InitialAdminPassword);
            Assert.AreEqual(HttpStatusCode.OK, adminLogin.StatusCode);
            using var passwordChanged = await adminSession.ChangePasswordAsync(
                HimiFlowWebApplicationFactory.InitialAdminPassword,
                HimiFlowWebApplicationFactory.ChangedAdminPassword);
            Assert.AreEqual(HttpStatusCode.OK, passwordChanged.StatusCode);
            await adminSession.RefreshCsrfAsync();
            using var installed = await adminSession.Client.PostAsJsonAsync(
                "/api/admin/license",
                new LicenseInstallRequest(licenseKey));
            Assert.AreEqual(HttpStatusCode.OK, installed.StatusCode);
        }
        else
        {
            await factory.SetInstalledLicenseAsync(licenseKey);
        }

        var employeeSession = CreateSession(factory);
        using var login = await employeeSession.LoginAsync(
            $"license.{expectedStatus.ToLowerInvariant()}",
            EmployeePassword);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        var status = await employeeSession.Client.GetFromJsonAsync<LicenseStatusResponse>("/api/license/status");
        Assert.IsNotNull(status);
        Assert.AreEqual(expectedStatus, status.Status);

        var masterData = await ReadMasterDataAsync(factory);
        Assert.AreEqual(employee.TeamId, masterData.TeamId);
        using var write = await employeeSession.Client.PostAsJsonAsync(
            "/api/savings",
            new SavingsEntryCreateRequest
            {
                Month = new DateTime(2026, 8, 1),
                Kvnr = "C123456789",
                OldKvAmount = 100m,
                NewKvAmount = 50m,
                TeamId = masterData.TeamId,
                SavingReasonId = masterData.ReasonId,
                ProductGroupId = masterData.ProductGroupId
            });

        Assert.AreEqual(
            writeAllowed ? HttpStatusCode.Created : HttpStatusCode.Forbidden,
            write.StatusCode);
        if (!writeAllowed)
        {
            StringAssert.Contains(await write.Content.ReadAsStringAsync(), "LICENSE_READ_ONLY");
        }

        if (expectedStatus == LicenseStatuses.Expired)
        {
            var recoveryAdmin = CreateSession(factory);
            using var recoveryLogin = await recoveryAdmin.LoginAsync(
                HimiFlowWebApplicationFactory.InitialAdminUserName,
                HimiFlowWebApplicationFactory.InitialAdminPassword);
            Assert.AreEqual(HttpStatusCode.OK, recoveryLogin.StatusCode);
            using var recoveryPassword = await recoveryAdmin.ChangePasswordAsync(
                HimiFlowWebApplicationFactory.InitialAdminPassword,
                HimiFlowWebApplicationFactory.ChangedAdminPassword);
            Assert.AreEqual(HttpStatusCode.OK, recoveryPassword.StatusCode);
            await recoveryAdmin.RefreshCsrfAsync();
            var activeKey = CreateLicense(rsa, now.AddDays(-1), now.AddDays(30), now.AddDays(60));
            using var renewed = await recoveryAdmin.Client.PostAsJsonAsync(
                "/api/admin/license",
                new LicenseInstallRequest(activeKey));
            Assert.AreEqual(HttpStatusCode.OK, renewed.StatusCode);

            using var retry = await employeeSession.Client.PostAsJsonAsync(
                "/api/savings",
                new SavingsEntryCreateRequest
                {
                    Month = new DateTime(2026, 9, 1),
                    Kvnr = "D123456789",
                    OldKvAmount = 100m,
                    NewKvAmount = 50m,
                    TeamId = masterData.TeamId,
                    SavingReasonId = masterData.ReasonId,
                    ProductGroupId = masterData.ProductGroupId
                });
            Assert.AreEqual(HttpStatusCode.Created, retry.StatusCode);
        }
    }

    [TestMethod]
    public async Task MaxUsersRejectsAnotherActiveUserOverRealHttp()
    {
        using var rsa = RSA.Create(2048);
        using var factory = new HimiFlowWebApplicationFactory(new Dictionary<string, string?>
        {
            ["License:EnforcementEnabled"] = "true",
            ["License:InstallationId"] = InstallationId,
            ["License:PublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem()
        });
        var admin = CreateSession(factory);
        using var login = await admin.LoginAsync(
            HimiFlowWebApplicationFactory.InitialAdminUserName,
            HimiFlowWebApplicationFactory.InitialAdminPassword);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        using var changed = await admin.ChangePasswordAsync(
            HimiFlowWebApplicationFactory.InitialAdminPassword,
            HimiFlowWebApplicationFactory.ChangedAdminPassword);
        Assert.AreEqual(HttpStatusCode.OK, changed.StatusCode);
        await admin.RefreshCsrfAsync();
        var now = DateTime.UtcNow;
        var license = CreateLicense(rsa, now.AddDays(-1), now.AddDays(30), now.AddDays(60), maxUsers: 1);
        using var installed = await admin.Client.PostAsJsonAsync(
            "/api/admin/license",
            new LicenseInstallRequest(license));
        Assert.AreEqual(HttpStatusCode.OK, installed.StatusCode);

        var masterData = await ReadMasterDataAsync(factory);
        using var create = await admin.Client.PostAsJsonAsync(
            "/api/user-management",
            new
            {
                userName = "limit.employee",
                displayName = "Limit Employee",
                roleName = ApplicationRoles.Mitarbeiter,
                teamId = masterData.TeamId
            });

        Assert.AreEqual(HttpStatusCode.Conflict, create.StatusCode);
        StringAssert.Contains(await create.Content.ReadAsStringAsync(), "LICENSE_MAX_USERS_REACHED");
    }

    private static string CreateLicense(
        RSA rsa,
        DateTime validFrom,
        DateTime validUntil,
        DateTime graceUntil,
        int maxUsers = 10)
    {
        var payload = new
        {
            licenseId = Guid.NewGuid().ToString("N"),
            customerName = "Integration Customer",
            product = "HimiFlow",
            validFrom,
            validUntil,
            graceUntil,
            maxUsers,
            features = Array.Empty<string>(),
            installationId = InstallationId
        };
        var payloadSegment = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(payloadSegment),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return $"HIMIFLOW-LICENSE-V1.{payloadSegment}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static HttpApiSession CreateSession(HimiFlowWebApplicationFactory factory) =>
        new(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        }));

    private static async Task<(int TeamId, int ReasonId, int ProductGroupId)> ReadMasterDataAsync(
        HimiFlowWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (
            await db.Teams.Select(item => item.Id).FirstAsync(),
            await db.SavingReasons.Select(item => item.Id).FirstAsync(),
            await db.ProductGroups.Select(item => item.Id).FirstAsync());
    }
}
