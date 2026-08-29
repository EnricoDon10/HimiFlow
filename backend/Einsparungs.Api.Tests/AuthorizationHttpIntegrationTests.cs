using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class AuthorizationHttpIntegrationTests
{
    private const string EmployeePassword = "E7@tV2!qR9#kM4$p";
    private const string OtherEmployeePassword = "J6#rN3@wT8!yQ2%v";
    private const string FachAdminPassword = "F4!zL8@qS2#vR7%m";

    [TestMethod]
    public async Task RoleMatrixSeparatesEmployeeFachAdminAndSystemAdminEndpoints()
    {
        using var factory = new HimiFlowWebApplicationFactory();
        var employee = await factory.AddUserAsync(
            "role.employee",
            EmployeePassword,
            ApplicationRoles.Mitarbeiter);
        var otherEmployee = await factory.AddUserAsync(
            "role.other",
            OtherEmployeePassword,
            ApplicationRoles.Mitarbeiter);
        await factory.AddUserAsync(
            "role.fachadmin",
            FachAdminPassword,
            ApplicationRoles.FachAdmin);
        var ownEntry = await factory.AddSavingsAsync(employee);
        var foreignEntry = await factory.AddSavingsAsync(otherEmployee, "B987654321");

        var employeeSession = CreateSession(factory);
        using var employeeLogin = await employeeSession.LoginAsync("role.employee", EmployeePassword);
        Assert.AreEqual(HttpStatusCode.OK, employeeLogin.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await employeeSession.Client.GetAsync("/api/savings/my")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await employeeSession.Client.GetAsync($"/api/savings/{ownEntry.Id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await employeeSession.Client.GetAsync($"/api/savings/{foreignEntry.Id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await employeeSession.Client.GetAsync("/api/savings/all")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await employeeSession.Client.GetAsync("/api/exports/savings.csv")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await employeeSession.Client.GetAsync("/api/user-management")).StatusCode);

        var fachSession = CreateSession(factory);
        using var fachLogin = await fachSession.LoginAsync("role.fachadmin", FachAdminPassword);
        Assert.AreEqual(HttpStatusCode.OK, fachLogin.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await fachSession.Client.GetAsync("/api/savings/all")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await fachSession.Client.GetAsync("/api/exports/savings.csv")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await fachSession.Client.GetAsync("/api/user-management")).StatusCode);

        var systemSession = CreateSession(factory);
        using var systemLogin = await systemSession.LoginAsync(
            HimiFlowWebApplicationFactory.InitialAdminUserName,
            HimiFlowWebApplicationFactory.InitialAdminPassword);
        Assert.AreEqual(HttpStatusCode.OK, systemLogin.StatusCode);
        using var changed = await systemSession.ChangePasswordAsync(
            HimiFlowWebApplicationFactory.InitialAdminPassword,
            HimiFlowWebApplicationFactory.ChangedAdminPassword);
        Assert.AreEqual(HttpStatusCode.OK, changed.StatusCode);
        await systemSession.RefreshCsrfAsync();
        Assert.AreEqual(HttpStatusCode.OK, (await systemSession.Client.GetAsync("/api/user-management")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await systemSession.Client.GetAsync("/api/savings/my")).StatusCode);
    }

    [TestMethod]
    public async Task SavingsUpdateWithStaleVersionReturnsConflictAndPreservesFirstUpdate()
    {
        using var factory = new HimiFlowWebApplicationFactory();
        var employee = await factory.AddUserAsync(
            "concurrency.employee",
            EmployeePassword,
            ApplicationRoles.Mitarbeiter);
        var entry = await factory.AddSavingsAsync(employee);
        var session = CreateSession(factory);
        using var login = await session.LoginAsync("concurrency.employee", EmployeePassword);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);

        var initial = await session.Client.GetFromJsonAsync<SavingsEntryResponse>($"/api/savings/{entry.Id}");
        Assert.IsNotNull(initial);
        var request = new SavingsEntryUpdateRequest
        {
            Month = initial.Month,
            Kvnr = initial.Kvnr,
            OldKvAmount = 120m,
            NewKvAmount = 40m,
            TeamId = initial.TeamId,
            SavingReasonId = initial.SavingReasonId,
            ProductGroupId = initial.ProductGroupId,
            ExpectedVersion = initial.Version
        };

        using var first = await session.Client.PutAsJsonAsync($"/api/savings/{entry.Id}", request);
        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);
        using var stale = await session.Client.PutAsJsonAsync($"/api/savings/{entry.Id}", request);
        Assert.AreEqual(HttpStatusCode.Conflict, stale.StatusCode);
        StringAssert.Contains(await stale.Content.ReadAsStringAsync(), "CONCURRENCY_CONFLICT");

        var persisted = await session.Client.GetFromJsonAsync<SavingsEntryResponse>($"/api/savings/{entry.Id}");
        Assert.IsNotNull(persisted);
        Assert.AreEqual(80m, persisted.SavingAmount);
        Assert.AreEqual(initial.Version + 1, persisted.Version);
    }

    [TestMethod]
    public async Task PasswordResetInvalidatesAlreadyIssuedSession()
    {
        using var factory = new HimiFlowWebApplicationFactory();
        var employee = await factory.AddUserAsync(
            "reset.employee",
            EmployeePassword,
            ApplicationRoles.Mitarbeiter);
        var employeeSession = CreateSession(factory);
        using var employeeLogin = await employeeSession.LoginAsync("reset.employee", EmployeePassword);
        Assert.AreEqual(HttpStatusCode.OK, employeeLogin.StatusCode);

        var adminSession = CreateSession(factory);
        using var adminLogin = await adminSession.LoginAsync(
            HimiFlowWebApplicationFactory.InitialAdminUserName,
            HimiFlowWebApplicationFactory.InitialAdminPassword);
        Assert.AreEqual(HttpStatusCode.OK, adminLogin.StatusCode);
        using var changed = await adminSession.ChangePasswordAsync(
            HimiFlowWebApplicationFactory.InitialAdminPassword,
            HimiFlowWebApplicationFactory.ChangedAdminPassword);
        Assert.AreEqual(HttpStatusCode.OK, changed.StatusCode);
        await adminSession.RefreshCsrfAsync();

        using var reset = await adminSession.Client.PostAsync(
            $"/api/user-management/{employee.Id}/reset-password",
            content: null);
        Assert.AreEqual(HttpStatusCode.OK, reset.StatusCode);

        using var me = await employeeSession.Client.GetAsync("/api/auth/me");
        Assert.AreEqual(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [TestMethod]
    public async Task UserCreatedBySystemAdminMustChangeTemporaryPasswordBeforeBusinessAccess()
    {
        using var factory = new HimiFlowWebApplicationFactory();
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

        using var created = await admin.Client.PostAsJsonAsync(
            "/api/user-management",
            new
            {
                userName = "temporary.employee",
                displayName = "Temporary Employee",
                roleName = ApplicationRoles.Mitarbeiter,
                teamId = await factory.GetFirstTeamIdAsync()
            });
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        using var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var temporaryPassword = document.RootElement.GetProperty("temporaryPassword").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(temporaryPassword));

        var employee = CreateSession(factory);
        using var employeeLogin = await employee.LoginAsync("temporary.employee", temporaryPassword!);
        Assert.AreEqual(HttpStatusCode.OK, employeeLogin.StatusCode);
        var loginResponse = await employeeLogin.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.IsNotNull(loginResponse);
        Assert.IsTrue(loginResponse.MustChangePassword);
        using var blocked = await employee.Client.GetAsync("/api/savings/my");
        Assert.AreEqual(HttpStatusCode.Forbidden, blocked.StatusCode);

        using var personalPassword = await employee.ChangePasswordAsync(
            temporaryPassword!,
            "N5@xQ8!vR2#kT7%m");
        Assert.AreEqual(HttpStatusCode.OK, personalPassword.StatusCode);
        await employee.RefreshCsrfAsync();
        using var allowed = await employee.Client.GetAsync("/api/savings/my");
        Assert.AreEqual(HttpStatusCode.OK, allowed.StatusCode);
    }

    private static HttpApiSession CreateSession(HimiFlowWebApplicationFactory factory)
    {
        return new HttpApiSession(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        }));
    }
}
