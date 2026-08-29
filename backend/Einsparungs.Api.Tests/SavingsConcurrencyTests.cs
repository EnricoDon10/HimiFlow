using System.Security.Claims;
using Einsparungs.Api.Controllers;
using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class SavingsConcurrencyTests
{
    [TestMethod]
    public async Task Update_WithCurrentVersion_UpdatesEntryAndIncrementsVersion()
    {
        await using var fixture = await SavingsFixture.CreateAsync(version: 5);

        var result = await fixture.Controller.Update(
            fixture.EntryId,
            fixture.CreateUpdateRequest(expectedVersion: 5, newKvAmount: 60m));

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);
        var response = ok.Value as SavingsEntryResponse;
        Assert.IsNotNull(response);
        Assert.AreEqual(6, response.Version);
        Assert.AreEqual(40m, response.SavingAmount);

        var stored = await fixture.Db.SavingsEntries.AsNoTracking().SingleAsync();
        Assert.AreEqual(6, stored.Version);
        Assert.AreEqual(60m, stored.NewKvAmount);
    }

    [TestMethod]
    public async Task Update_WithStaleVersion_ReturnsConflictProblemDetails()
    {
        await using var fixture = await SavingsFixture.CreateAsync(version: 6);

        var result = await fixture.Controller.Update(
            fixture.EntryId,
            fixture.CreateUpdateRequest(expectedVersion: 5, newKvAmount: 10m));

        var conflict = result.Result as ConflictObjectResult;
        Assert.IsNotNull(conflict);
        var problem = conflict.Value as ProblemDetails;
        Assert.IsNotNull(problem);
        Assert.AreEqual(StatusCodes.Status409Conflict, problem.Status);
        Assert.AreEqual("CONCURRENCY_CONFLICT", problem.Extensions["code"]);
    }

    [TestMethod]
    public async Task Update_WithStaleVersion_LeavesEntryAndAuditUnchanged()
    {
        await using var fixture = await SavingsFixture.CreateAsync(version: 6);

        await fixture.Controller.Update(
            fixture.EntryId,
            fixture.CreateUpdateRequest(expectedVersion: 5, newKvAmount: 10m));

        var stored = await fixture.Db.SavingsEntries.AsNoTracking().SingleAsync();
        Assert.AreEqual(6, stored.Version);
        Assert.AreEqual(75m, stored.NewKvAmount);
        Assert.AreEqual(0, await fixture.Db.AuditLogs.CountAsync());
    }

    private sealed class SavingsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private SavingsFixture(
            SqliteConnection connection,
            AppDbContext db,
            SavingsController controller,
            Guid entryId,
            int teamId,
            int savingReasonId,
            int productGroupId)
        {
            this.connection = connection;
            Db = db;
            Controller = controller;
            EntryId = entryId;
            TeamId = teamId;
            SavingReasonId = savingReasonId;
            ProductGroupId = productGroupId;
        }

        public AppDbContext Db { get; }
        public SavingsController Controller { get; }
        public Guid EntryId { get; }
        private int TeamId { get; }
        private int SavingReasonId { get; }
        private int ProductGroupId { get; }

        public static async Task<SavingsFixture> CreateAsync(int version)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var team = new Team { Code = "T01", Name = "Team 1", DisplayName = "Team 1" };
            var reason = new SavingReason { Name = "Verhandlung" };
            var productGroup = new ProductGroup { DisplayValue = "PG 01" };
            var user = new AppUser
            {
                UserName = "employee",
                NormalizedUserName = "EMPLOYEE",
                DisplayName = "Test Mitarbeiter",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                Team = team,
                MustChangePassword = false
            };
            var entry = new SavingsEntry
            {
                Month = new DateTime(2026, 8, 1),
                Kvnr = "A123456789",
                OldKvAmount = 100m,
                NewKvAmount = 75m,
                SavingAmount = 25m,
                Team = team,
                SavingReason = reason,
                ProductGroup = productGroup,
                CreatedByUser = user,
                Version = version
            };

            db.AddRange(team, reason, productGroup, user, entry);
            await db.SaveChangesAsync();

            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Role, ApplicationRoles.Mitarbeiter)
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

            return new SavingsFixture(
                connection,
                db,
                controller,
                entry.Id,
                team.Id,
                reason.Id,
                productGroup.Id);
        }

        public SavingsEntryUpdateRequest CreateUpdateRequest(int expectedVersion, decimal newKvAmount)
        {
            return new SavingsEntryUpdateRequest
            {
                ExpectedVersion = expectedVersion,
                Month = new DateTime(2026, 8, 1),
                Kvnr = "A123456789",
                OldKvAmount = 100m,
                NewKvAmount = newKvAmount,
                TeamId = TeamId,
                SavingReasonId = SavingReasonId,
                ProductGroupId = ProductGroupId
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
