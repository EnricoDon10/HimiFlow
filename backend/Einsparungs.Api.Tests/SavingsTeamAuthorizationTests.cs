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
public sealed class SavingsTeamAuthorizationTests
{
    [TestMethod]
    public async Task Employee_CreatesEntryForOwnTeam()
    {
        await using var fixture = await TeamFixture.CreateAsync(ApplicationRoles.Mitarbeiter);

        var result = await fixture.Controller.Create(fixture.Request(fixture.OwnTeamId));

        Assert.IsInstanceOfType<CreatedAtActionResult>(result.Result);
        var entry = await fixture.Db.SavingsEntries.AsNoTracking().SingleAsync();
        Assert.AreEqual(fixture.OwnTeamId, entry.TeamId);
    }

    [TestMethod]
    public async Task Employee_CannotCreateEntryForAnotherTeam()
    {
        await using var fixture = await TeamFixture.CreateAsync(ApplicationRoles.Mitarbeiter);

        var result = await fixture.Controller.Create(fixture.Request(fixture.OtherTeamId));

        var forbidden = result.Result as ObjectResult;
        Assert.IsNotNull(forbidden);
        Assert.AreEqual(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        var problem = forbidden.Value as ProblemDetails;
        Assert.IsNotNull(problem);
        Assert.AreEqual("TEAM_SCOPE_VIOLATION", problem.Extensions["code"]);
        Assert.AreEqual(0, await fixture.Db.SavingsEntries.CountAsync());
        Assert.AreEqual(0, await fixture.Db.AuditLogs.CountAsync());
    }

    [TestMethod]
    public async Task FachAdmin_CreatesEntryForAnotherActiveTeam()
    {
        await using var fixture = await TeamFixture.CreateAsync(ApplicationRoles.FachAdmin);

        var result = await fixture.Controller.Create(fixture.Request(fixture.OtherTeamId));

        Assert.IsInstanceOfType<CreatedAtActionResult>(result.Result);
        var entry = await fixture.Db.SavingsEntries.AsNoTracking().SingleAsync();
        Assert.AreEqual(fixture.OtherTeamId, entry.TeamId);
    }

    private sealed class TeamFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TeamFixture(
            SqliteConnection connection,
            AppDbContext db,
            SavingsController controller,
            int ownTeamId,
            int otherTeamId,
            int reasonId,
            int productGroupId)
        {
            this.connection = connection;
            Db = db;
            Controller = controller;
            OwnTeamId = ownTeamId;
            OtherTeamId = otherTeamId;
            ReasonId = reasonId;
            ProductGroupId = productGroupId;
        }

        public AppDbContext Db { get; }
        public SavingsController Controller { get; }
        public int OwnTeamId { get; }
        public int OtherTeamId { get; }
        private int ReasonId { get; }
        private int ProductGroupId { get; }

        public static async Task<TeamFixture> CreateAsync(string role)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var ownTeam = new Team { Code = "T01", Name = "Team 1", DisplayName = "Team 1" };
            var otherTeam = new Team { Code = "T02", Name = "Team 2", DisplayName = "Team 2" };
            var reason = new SavingReason { Name = "Grund" };
            var product = new ProductGroup { DisplayValue = "PG" };
            var user = new AppUser
            {
                UserName = "writer",
                NormalizedUserName = "WRITER",
                DisplayName = "Test Writer",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                Team = ownTeam,
                MustChangePassword = false
            };
            db.AddRange(ownTeam, otherTeam, reason, product, user);
            await db.SaveChangesAsync();

            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Role, role)
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
            return new TeamFixture(
                connection,
                db,
                controller,
                ownTeam.Id,
                otherTeam.Id,
                reason.Id,
                product.Id);
        }

        public SavingsEntryCreateRequest Request(int teamId)
        {
            return new SavingsEntryCreateRequest
            {
                Month = new DateTime(2026, 8, 1),
                Kvnr = "A123456789",
                OldKvAmount = 100m,
                NewKvAmount = 50m,
                TeamId = teamId,
                SavingReasonId = ReasonId,
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
