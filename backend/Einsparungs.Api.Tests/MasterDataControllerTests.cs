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
using System.Text.Json;

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

        var activeDuplicate = await fixture.Controller.CreateSavingReason(
            new SavingReasonSaveRequest("Vertragsoptimierung"), CancellationToken.None);
        Assert.IsInstanceOfType(activeDuplicate.Result, typeof(BadRequestObjectResult));

        var deactivate = await fixture.Controller.DeactivateSavingReason(reason.Id, CancellationToken.None);
        Assert.IsTrue(((OkObjectResult)deactivate.Result!).Value is SavingReasonResponse { IsActive: false });

        var lookup = await fixture.Controller.GetSavingReasons(CancellationToken.None);
        var activeReasons = ((OkObjectResult)lookup.Result!).Value as IReadOnlyList<SavingReasonResponse>;
        Assert.IsNotNull(activeReasons);
        Assert.IsFalse(activeReasons.Any(item => item.Id == reason.Id));
        var managedLookup = await fixture.Controller.GetManagedSavingReasons(CancellationToken.None);
        var managedReasons = ((OkObjectResult)managedLookup.Result!).Value as IReadOnlyList<SavingReasonResponse>;
        Assert.IsNotNull(managedReasons);
        Assert.IsTrue(managedReasons.Any(item => item.Id == reason.Id && !item.IsActive));
        Assert.IsTrue(await fixture.Db.AuditLogs.AnyAsync(log => log.EntityName == "SavingReason" && log.Action == "Deactivated"));
    }

    [TestMethod]
    public async Task InactiveDuplicateReturnsReactivationConflictAndActivationRestoresLookup()
    {
        await using var fixture = await Fixture.CreateAsync();

        var create = await fixture.Controller.CreateSavingReason(
            new SavingReasonSaveRequest("Kompressionsartikel"), CancellationToken.None);
        var reason = ((CreatedAtActionResult)create.Result!).Value as SavingReasonResponse;
        Assert.IsNotNull(reason);
        await fixture.Controller.DeactivateSavingReason(reason.Id, CancellationToken.None);

        var duplicate = await fixture.Controller.CreateSavingReason(
            new SavingReasonSaveRequest(" kompressionsARTIKEL "), CancellationToken.None);
        var conflict = duplicate.Result as ConflictObjectResult;
        Assert.IsNotNull(conflict);
        var problem = conflict.Value as ProblemDetails;
        Assert.IsNotNull(problem);
        Assert.AreEqual("MASTER_DATA_INACTIVE_EXISTS", problem.Extensions["code"]);
        Assert.AreEqual(reason.Id, problem.Extensions["id"]);

        var activate = await fixture.Controller.ActivateSavingReason(reason.Id, CancellationToken.None);
        Assert.IsTrue(((OkObjectResult)activate.Result!).Value is SavingReasonResponse { IsActive: true });
        var lookup = await fixture.Controller.GetSavingReasons(CancellationToken.None);
        var activeReasons = ((OkObjectResult)lookup.Result!).Value as IReadOnlyList<SavingReasonResponse>;
        Assert.IsNotNull(activeReasons);
        Assert.IsTrue(activeReasons.Any(item => item.Id == reason.Id));
    }

    [TestMethod]
    public async Task InactiveTeamAndProductGroupDuplicatesReturnSpecificConflict()
    {
        await using var fixture = await Fixture.CreateAsync();

        var teamCreate = await fixture.Controller.CreateTeam(
            new TeamSaveRequest(OrganizationUnit: "ORG-99 - Test"), CancellationToken.None);
        var team = ((CreatedAtActionResult)teamCreate.Result!).Value as TeamResponse;
        Assert.IsNotNull(team);
        await fixture.Controller.DeactivateTeam(team.Id, CancellationToken.None);
        var teamDuplicate = await fixture.Controller.CreateTeam(
            new TeamSaveRequest(OrganizationUnit: "org-99 - TEST"), CancellationToken.None);
        Assert.AreEqual("MASTER_DATA_INACTIVE_EXISTS", ((ProblemDetails)((ConflictObjectResult)teamDuplicate.Result!).Value!).Extensions["code"]);

        var groupCreate = await fixture.Controller.CreateProductGroup(
            new ProductGroupSaveRequest("Kompressionsartikel"), CancellationToken.None);
        var group = ((CreatedAtActionResult)groupCreate.Result!).Value as ProductGroupResponse;
        Assert.IsNotNull(group);
        await fixture.Controller.DeactivateProductGroup(group.Id, CancellationToken.None);
        var groupDuplicate = await fixture.Controller.CreateProductGroup(
            new ProductGroupSaveRequest(" kompressionsARTIKEL "), CancellationToken.None);
        Assert.AreEqual("MASTER_DATA_INACTIVE_EXISTS", ((ProblemDetails)((ConflictObjectResult)groupDuplicate.Result!).Value!).Extensions["code"]);
    }

    [TestMethod]
    public async Task UnicodeDuplicateChecks_AreProviderIndependentAndKeepVisibleSpelling()
    {
        await using var fixture = await Fixture.CreateAsync();

        var reasonCreate = await fixture.Controller.CreateSavingReason(
            new SavingReasonSaveRequest("Änderung"), CancellationToken.None);
        var reason = ((CreatedAtActionResult)reasonCreate.Result!).Value as SavingReasonResponse;
        Assert.IsNotNull(reason);
        Assert.AreEqual("Änderung", reason.Name);
        var reasonDuplicate = await fixture.Controller.CreateSavingReason(
            new SavingReasonSaveRequest("  änderung  "), CancellationToken.None);
        Assert.IsInstanceOfType(reasonDuplicate.Result, typeof(BadRequestObjectResult));

        var groupCreate = await fixture.Controller.CreateProductGroup(
            new ProductGroupSaveRequest("ÜBERNAHME"), CancellationToken.None);
        var group = ((CreatedAtActionResult)groupCreate.Result!).Value as ProductGroupResponse;
        Assert.IsNotNull(group);
        Assert.AreEqual("ÜBERNAHME", group.DisplayValue);
        var groupDuplicate = await fixture.Controller.CreateProductGroup(
            new ProductGroupSaveRequest(" übernahme "), CancellationToken.None);
        Assert.IsInstanceOfType(groupDuplicate.Result, typeof(BadRequestObjectResult));

        var teamCreate = await fixture.Controller.CreateTeam(
            new TeamSaveRequest(OrganizationUnit: "ÜBERNAHME"), CancellationToken.None);
        Assert.IsInstanceOfType(teamCreate.Result, typeof(CreatedAtActionResult));
        var teamDuplicate = await fixture.Controller.CreateTeam(
            new TeamSaveRequest(OrganizationUnit: " übernahme "), CancellationToken.None);
        Assert.IsInstanceOfType(teamDuplicate.Result, typeof(BadRequestObjectResult));
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
    public async Task AuditChangedFieldsContainsOnlyFieldsThatActuallyChanged()
    {
        await using var fixture = await Fixture.CreateAsync();
        var create = await fixture.Controller.CreateSavingReason(
            new SavingReasonSaveRequest("Original"), CancellationToken.None);
        var reason = ((CreatedAtActionResult)create.Result!).Value as SavingReasonResponse;
        Assert.IsNotNull(reason);

        await fixture.Controller.UpdateSavingReason(
            reason.Id,
            new SavingReasonSaveRequest("Neu"),
            CancellationToken.None);
        var updateAudit = await fixture.Db.AuditLogs
            .Where(log => log.EntityName == "SavingReason" && log.Action == "Updated")
            .OrderByDescending(log => log.ChangedAt)
            .FirstAsync();
        CollectionAssert.AreEqual(new[] { "name" }, JsonSerializer.Deserialize<string[]>(updateAudit.ChangedFieldsJson!)!);

        await fixture.Controller.DeactivateSavingReason(reason.Id, CancellationToken.None);
        var deactivateAudit = await fixture.Db.AuditLogs
            .Where(log => log.EntityName == "SavingReason" && log.Action == "Deactivated")
            .OrderByDescending(log => log.ChangedAt)
            .FirstAsync();
        CollectionAssert.AreEqual(new[] { "isActive" }, JsonSerializer.Deserialize<string[]>(deactivateAudit.ChangedFieldsJson!)!);
    }

    [TestMethod]
    public async Task FachAdminCanCreateAndDeactivateSingleFieldOrganizationUnit()
    {
        await using var fixture = await Fixture.CreateAsync();

        var create = await fixture.Controller.CreateTeam(
            new TeamSaveRequest(OrganizationUnit: "ORG-01 - Hilfsmittelversorgung"),
            CancellationToken.None);
        var team = ((CreatedAtActionResult)create.Result!).Value as TeamResponse;
        Assert.IsNotNull(team);
        Assert.AreEqual("ORG-01 - Hilfsmittelversorgung", team.DisplayName);

        var deactivate = await fixture.Controller.DeactivateTeam(team.Id, CancellationToken.None);

        Assert.IsTrue(((OkObjectResult)deactivate.Result!).Value is TeamResponse { IsActive: false });
        Assert.IsFalse(await fixture.Db.Teams.Where(item => item.Id == team.Id).Select(item => item.IsActive).SingleAsync());
        var managed = await fixture.Controller.GetManagedTeams(CancellationToken.None);
        var managedTeams = ((OkObjectResult)managed.Result!).Value as IReadOnlyList<TeamResponse>;
        Assert.IsNotNull(managedTeams);
        Assert.IsTrue(managedTeams.Any(item => item.Id == team.Id && !item.IsActive));
        var reactivated = await fixture.Controller.ActivateTeam(team.Id, CancellationToken.None);
        Assert.IsTrue(((OkObjectResult)reactivated.Result!).Value is TeamResponse { IsActive: true });
        Assert.IsTrue(await fixture.Db.AuditLogs.AnyAsync(log => log.EntityName == "Team" && log.EntityId == team.Id.ToString() && log.Action == "Deactivated"));
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
