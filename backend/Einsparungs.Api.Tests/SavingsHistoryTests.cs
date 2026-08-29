using System.Reflection;
using System.Security.Claims;
using Einsparungs.Api.Controllers;
using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class SavingsHistoryTests
{
    [TestMethod]
    public async Task History_ReturnsOnlyWhitelistedFieldsAndMasksKvnr()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var actor = new AppUser
        {
            UserName = "fachadmin",
            NormalizedUserName = "FACHADMIN",
            DisplayName = "Fachliche Leitung",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            MustChangePassword = false
        };
        db.Users.Add(actor);
        await db.SaveChangesAsync();

        var savingsId = Guid.NewGuid();
        db.AuditLogs.Add(new AuditLog
        {
            EntityName = "SavingsEntry",
            EntityId = savingsId.ToString(),
            Action = "Updated",
            ChangedByUserId = actor.Id,
            ChangedAt = new DateTime(2026, 8, 29, 10, 30, 0, DateTimeKind.Utc),
            OldValuesJson = """{"kvnr":"A123456789","oldKvAmount":100,"version":1,"createdByUserId":"secret-user-id"}""",
            NewValuesJson = """{"kvnr":"B987654321","oldKvAmount":80,"version":2,"passwordHash":"never expose"}"""
        });
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()),
                new Claim(ClaimTypes.Role, ApplicationRoles.FachAdmin)
            ],
            "TestCookie");
        var controller = new SavingsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };

        var result = await controller.GetHistory(savingsId, CancellationToken.None);
        var history = ((OkObjectResult)result.Result!).Value as IReadOnlyList<SavingsHistoryEntryResponse>;

        Assert.IsNotNull(history);
        Assert.HasCount(1, history);
        Assert.HasCount(2, history[0].Changes);
        Assert.AreEqual("A******789", history[0].Changes.Single(x => x.Field == "kvnr").OldValue);
        Assert.AreEqual("B******321", history[0].Changes.Single(x => x.Field == "kvnr").NewValue);
        Assert.IsFalse(history[0].Changes.Any(x => x.Field is "version" or "createdByUserId" or "passwordHash"));
    }

    [TestMethod]
    public void History_IsRestrictedToFachAdminOnly()
    {
        var method = typeof(SavingsController).GetMethod(
            nameof(SavingsController.GetHistory),
            BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(method);
        var attribute = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.IsNotNull(attribute);
        Assert.AreEqual(ApplicationRoles.FachAdmin, attribute.Roles);
    }
}
