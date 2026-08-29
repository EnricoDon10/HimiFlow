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

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class MasterDataControllerTests
{
    [TestMethod]
    public async Task FachAdminCanCreateAndDeactivateMasterDataAndLookupsHideInactiveValues()
    {
        await using var fixture = await Fixture.CreateAsync();

        var teamResult = await fixture.Controller.CreateTeam(
            new TeamSaveRequest("3410", "Bochum 1"), CancellationToken.None);
        var team = ((CreatedAtActionResult)teamResult.Result!).Value as TeamResponse;
        Assert.IsNotNull(team);
        Assert.AreEqual("Bochum 1 (3410)", team.DisplayName);

        var reasonResult = await fixture.Controller.CreateSavingReason(
            new SavingReasonSaveRequest("Vertragsoptimierung"), CancellationToken.None);
        var reason = ((CreatedAtActionResult)reasonResult.Result!).Value as SavingReasonResponse;
        Assert.IsNotNull(reason);

        var deactivate = await fixture.Controller.DeactivateSavingReason(reason.Id, CancellationToken.None);
        Assert.IsTrue(((OkObjectResult)deactivate.Result!).Value is SavingReasonResponse { IsActive: false });

        var lookup = await fixture.Controller.GetSavingReasons(CancellationToken.None);
        var activeReasons = ((OkObjectResult)lookup.Result!).Value as IReadOnlyList<SavingReasonResponse>;
        Assert.IsNotNull(activeReasons);
        Assert.IsFalse(activeReasons.Any(item => item.Id == reason.Id));
        Assert.IsTrue(await fixture.Db.AuditLogs.AnyAsync(log => log.EntityName == "SavingReason" && log.Action == "Deactivated"));
    }

    [TestMethod]
    public async Task TeamWithActiveUsersCannotBeDeactivated()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.Controller.DeactivateTeam(fixture.TeamId, CancellationToken.None);

        var conflict = result.Result as ObjectResult;
        Assert.IsNotNull(conflict);
        Assert.AreEqual(StatusCodes.Status409Conflict, conflict.StatusCode);
        var problem = conflict.Value as ProblemDetails;
        Assert.IsNotNull(problem);
        Assert.AreEqual("TEAM_HAS_ACTIVE_USERS", problem.Extensions["code"]);
        Assert.AreEqual(1, problem.Extensions["activeUserCount"]);
    }

    [TestMethod]
    public async Task FachAdminCanCreateAndDeleteSingleFieldOrganizationUnit()
    {
        await using var fixture = await Fixture.CreateAsync();

        var create = await fixture.Controller.CreateTeam(
            new TeamSaveRequest(OrganizationUnit: "ORG-01 - Hilfsmittelversorgung"),
            CancellationToken.None);
        var team = ((CreatedAtActionResult)create.Result!).Value as TeamResponse;
        Assert.IsNotNull(team);
        Assert.AreEqual("ORG-01 - Hilfsmittelversorgung", team.DisplayName);

        var delete = await fixture.Controller.DeleteTeam(team.Id, CancellationToken.None);

        Assert.IsInstanceOfType(delete, typeof(NoContentResult));
        Assert.IsFalse(await fixture.Db.Teams.Where(item => item.Id == team.Id).Select(item => item.IsActive).SingleAsync());
        var managed = await fixture.Controller.GetManagedTeams(CancellationToken.None);
        var managedTeams = ((OkObjectResult)managed.Result!).Value as IReadOnlyList<TeamResponse>;
        Assert.IsNotNull(managedTeams);
        Assert.IsFalse(managedTeams.Any(item => item.Id == team.Id));
        Assert.IsTrue(await fixture.Db.AuditLogs.AnyAsync(log => log.EntityName == "Team" && log.EntityId == team.Id.ToString() && log.Action == "Deleted"));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public AppDbContext Db { get; }
        public MasterDataController Controller { get; }
        public int TeamId { get; }

        private Fixture(SqliteConnection connection, AppDbContext db, MasterDataController controller, int teamId)
        {
            this.connection = connection;
            Db = db;
            Controller = controller;
            TeamId = teamId;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var team = new Team { Code = "T01", Name = "Team 1", DisplayName = "Team 1", IsActive = true };
            var admin = new AppUser
            {
                UserName = "fachadmin",
                NormalizedUserName = "FACHADMIN",
                DisplayName = "Fach Admin",
                Team = team,
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            db.AddRange(team, admin);
            await db.SaveChangesAsync();
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()), new Claim(ClaimTypes.Role, ApplicationRoles.FachAdmin)],
                "TestCookie");
            var controller = new MasterDataController(db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
                }
            };
            return new Fixture(connection, db, controller, team.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
