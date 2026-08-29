using System.Reflection;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class LicenseReadOnlyMiddlewareRouteTests
{
    [TestMethod]
    [DataRow("/api/auth/logout", true)]
    [DataRow("/api/auth/change-password", true)]
    [DataRow("/api/admin/license", true)]
    [DataRow("/api/operations/backups", true)]
    [DataRow("/api/user-management/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/reset-password", true)]
    [DataRow("/api/user-management/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/deactivate", true)]
    [DataRow("/api/user-management", false)]
    [DataRow("/api/user-management/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/role", false)]
    [DataRow("/api/user-management/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/activate", false)]
    [DataRow("/api/savings", false)]
    public void ExpiredLicense_AllowsOnlyMinimalRecoveryWrites(string path, bool expected)
    {
        var method = typeof(LicenseReadOnlyMiddleware).GetMethod(
            "IsRecoveryWriteAllowed",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;

        var actual = (bool)method.Invoke(null, [context.Request])!;

        Assert.AreEqual(expected, actual);
    }
}
