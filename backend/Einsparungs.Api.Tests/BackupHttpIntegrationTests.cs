using System.Net;
using System.Net.Http.Json;
using Einsparungs.Api.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class BackupHttpIntegrationTests
{
    [TestMethod]
    public async Task BackupStatus_WithNoExistingBackup_ReturnsOkAndValidState()
    {
        using var factory = new HimiFlowWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        var session = new HttpApiSession(client);

        var login = await session.LoginAsync(
            HimiFlowWebApplicationFactory.InitialAdminUserName,
            HimiFlowWebApplicationFactory.InitialAdminPassword);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);

        using var passwordChange = await session.ChangePasswordAsync(
            HimiFlowWebApplicationFactory.InitialAdminPassword,
            HimiFlowWebApplicationFactory.ChangedAdminPassword);
        Assert.AreEqual(HttpStatusCode.OK, passwordChange.StatusCode);

        var response = await client.GetAsync("/api/operations/backup-status");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<BackupStatusResponse>();

        Assert.IsNotNull(status);
        Assert.AreEqual("DISABLED", status.Status);
        Assert.AreEqual(0, status.AvailableBackups);
        Assert.IsTrue(status.IsMissing);
        Assert.IsFalse(status.IsOverdue);
    }
}
