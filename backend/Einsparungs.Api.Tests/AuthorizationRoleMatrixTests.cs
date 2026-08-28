using Einsparungs.Api.Controllers;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class AuthorizationRoleMatrixTests
{
    [TestMethod]
    public void Savings_SeparatesOwnAndAllViews()
    {
        var allSavings = typeof(SavingsController).GetMethod(nameof(SavingsController.GetAllSavings));

        var classRoles = typeof(SavingsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Roles;
        var allRoles = allSavings?
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Roles;

        Assert.AreEqual(ApplicationRoles.Mitarbeiter + "," + ApplicationRoles.FachAdmin, classRoles);
        Assert.AreEqual(ApplicationRoles.FachAdmin, allRoles);
    }

    [TestMethod]
    public void UserManagement_IsSystemAdminOnly()
    {
        var roles = typeof(UserManagementController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Roles;

        Assert.AreEqual(ApplicationRoles.SystemAdmin, roles);
    }

    [TestMethod]
    public void Exports_AreFachAdminOnly()
    {
        var roles = typeof(ExportsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Roles;

        Assert.AreEqual(ApplicationRoles.FachAdmin, roles);
    }

    [TestMethod]
    public void Audit_IsSystemAdminOnly()
    {
        var roles = typeof(AuditController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Roles;

        Assert.AreEqual(ApplicationRoles.SystemAdmin, roles);
    }
}
