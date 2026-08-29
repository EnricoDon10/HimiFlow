using System.Net;
using System.Net.Http.Json;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class AuthenticationHttpIntegrationTests
{
    [TestMethod]
    public async Task Login_WithCorrectPasswordSetsCookieAndReturnsMustChangePassword()
    {
        using var factory = new HimiFlowWebApplicationFactory();
        var lockoutState = await factory.GetLockoutStateAsync(HimiFlowWebApplicationFactory.InitialAdminUserName);
        Assert.IsFalse(
            lockoutState.IsLockedOut,
            $"Initial admin unexpectedly locked: failed={lockoutState.FailedCount}, end={lockoutState.LockoutEnd:O}, db={factory.GetConfiguredConnectionString()}");
        var session = CreateSession(factory);

        using var response = await session.LoginAsync(
            HimiFlowWebApplicationFactory.InitialAdminUserName,
            HimiFlowWebApplicationFactory.InitialAdminPassword);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(response.Headers.GetValues("Set-Cookie").Any(value => value.StartsWith("HimiFlow.Auth=", StringComparison.Ordinal)));
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.IsNotNull(login);
        Assert.IsTrue(login.MustChangePassword);
    }

    [TestMethod]
    public async Task Login_WithWrongPasswordReturnsUnauthorizedAndLocksAfterConfiguredFailures()
    {
        using var factory = new HimiFlowWebApplicationFactory();
        var session = CreateSession(factory);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failure = await session.LoginAsync(
                HimiFlowWebApplicationFactory.InitialAdminUserName,
                "F7@xQ2!mR9#vK4$p");
            Assert.AreEqual(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        using var locked = await session.LoginAsync(
            HimiFlowWebApplicationFactory.InitialAdminUserName,
            HimiFlowWebApplicationFactory.InitialAdminPassword);
        Assert.AreEqual(HttpStatusCode.Unauthorized, locked.StatusCode);
    }

    [TestMethod]
    public async Task ProtectedEndpointWithoutLoginReturnsUnauthorized()
    {
        using var factory = new HimiFlowWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/user-management");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task UnsafeRequestWithoutCsrfTokenIsRejected()
    {
        using var factory = new HimiFlowWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { userName = HimiFlowWebApplicationFactory.InitialAdminUserName, password = HimiFlowWebApplicationFactory.InitialAdminPassword });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task MustChangePasswordBlocksApiUntilPasswordWasChanged()
    {
        using var factory = new HimiFlowWebApplicationFactory();
        var session = CreateSession(factory);
        using var login = await session.LoginAsync(
            HimiFlowWebApplicationFactory.InitialAdminUserName,
            HimiFlowWebApplicationFactory.InitialAdminPassword);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);

        using var blocked = await session.Client.GetAsync("/api/user-management");
        Assert.AreEqual(HttpStatusCode.Forbidden, blocked.StatusCode);
        var blockedBody = await blocked.Content.ReadAsStringAsync();
        StringAssert.Contains(blockedBody, "PASSWORD_CHANGE_REQUIRED");

        using var changed = await session.ChangePasswordAsync(
            HimiFlowWebApplicationFactory.InitialAdminPassword,
            HimiFlowWebApplicationFactory.ChangedAdminPassword);
        Assert.AreEqual(HttpStatusCode.OK, changed.StatusCode);
        await session.RefreshCsrfAsync();

        using var allowed = await session.Client.GetAsync("/api/user-management");
        Assert.AreEqual(HttpStatusCode.OK, allowed.StatusCode);
    }

    [TestMethod]
    public async Task LogoutEndsSession()
    {
        using var factory = new HimiFlowWebApplicationFactory();
        var session = CreateSession(factory);
        using var login = await session.LoginAsync(
            HimiFlowWebApplicationFactory.InitialAdminUserName,
            HimiFlowWebApplicationFactory.InitialAdminPassword);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);

        using var logout = await session.Client.PostAsync("/api/auth/logout", content: null);
        Assert.AreEqual(HttpStatusCode.NoContent, logout.StatusCode);

        using var me = await session.Client.GetAsync("/api/auth/me");
        Assert.AreEqual(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [TestMethod]
    public async Task DeactivatedUserLosesExistingSession()
    {
        using var factory = new HimiFlowWebApplicationFactory();
        var user = await factory.AddUserAsync(
            "employee.active",
            "E7@tV2!qR9#kM4$p",
            ApplicationRoles.Mitarbeiter);
        var session = CreateSession(factory);
        using var login = await session.LoginAsync("employee.active", "E7@tV2!qR9#kM4$p");
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);

        await factory.SetUserActiveAsync(user.Id, false);
        using var me = await session.Client.GetAsync("/api/auth/me");

        Assert.AreEqual(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    private static HttpApiSession CreateSession(HimiFlowWebApplicationFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        return new HttpApiSession(client);
    }
}
