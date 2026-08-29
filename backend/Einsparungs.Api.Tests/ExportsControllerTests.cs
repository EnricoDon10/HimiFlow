using System.Security.Claims;
using System.Text;
using Einsparungs.Api.Controllers;
using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class ExportsControllerTests
{
    [TestMethod]
    public async Task CsvExportAppliesTeamFilterAndNeutralizesFormulaText()
    {
        await using var fixture = await Fixture.CreateAsync(maximumRows: 10);
        var result = await fixture.Controller.ExportSavingsCsv(
            new ExportSavingsQuery { TeamId = fixture.TeamId }, CancellationToken.None);

        Assert.IsInstanceOfType<EmptyResult>(result);
        fixture.ResponseBody.Position = 0;
        var csv = await new StreamReader(fixture.ResponseBody, Encoding.UTF8).ReadToEndAsync();
        StringAssert.Contains(csv, "'=Team");
        StringAssert.Contains(csv, "'=Reason");
        StringAssert.Contains(csv, "'=Product");
        Assert.IsFalse(csv.Contains("Other team", StringComparison.Ordinal));
        Assert.AreEqual(1, await fixture.Db.AuditLogs.CountAsync(log => log.EntityName == "SavingsExport"));
    }

    [TestMethod]
    public async Task ExportLimitReturnsProblemDetailsInsteadOfTruncating()
    {
        await using var fixture = await Fixture.CreateAsync(maximumRows: 1);
        var result = await fixture.Controller.ExportSavingsCsv(new ExportSavingsQuery(), CancellationToken.None);

        var response = result as ObjectResult;
        Assert.IsNotNull(response);
        Assert.AreEqual(StatusCodes.Status413PayloadTooLarge, response.StatusCode);
        var problem = response.Value as ProblemDetails;
        Assert.IsNotNull(problem);
        Assert.AreEqual("EXPORT_LIMIT_EXCEEDED", problem.Extensions["code"]);
        Assert.AreEqual(2, problem.Extensions["actualRows"]);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public AppDbContext Db { get; }
        public ExportsController Controller { get; }
        public MemoryStream ResponseBody { get; }
        public int TeamId { get; }

        private Fixture(SqliteConnection connection, AppDbContext db, ExportsController controller, MemoryStream responseBody, int teamId)
        {
            this.connection = connection;
            Db = db;
            Controller = controller;
            ResponseBody = responseBody;
            TeamId = teamId;
        }

        public static async Task<Fixture> CreateAsync(int maximumRows)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var team = new Team { Code = "E1", Name = "=Team", DisplayName = "=Team" };
            var otherTeam = new Team { Code = "E2", Name = "Other team", DisplayName = "Other team" };
            var reason = new SavingReason { Name = "=Reason" };
            var productGroup = new ProductGroup { DisplayValue = "=Product" };
            var user = new AppUser
            {
                UserName = "export.fachadmin",
                NormalizedUserName = "EXPORT.FACHADMIN",
                DisplayName = "Export FachAdmin",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                IsActive = true
            };
            db.AddRange(team, otherTeam, reason, productGroup, user);
            await db.SaveChangesAsync();
            db.SavingsEntries.AddRange(
                Entry(team, reason, productGroup, user, "A123456789"),
                Entry(otherTeam, reason, productGroup, user, "B123456789"));
            await db.SaveChangesAsync();

            var body = new MemoryStream();
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Role, ApplicationRoles.FachAdmin)
                ],
                "TestCookie");
            var controller = new ExportsController(db, new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Privacy:MaskKvnrInExports"] = "true",
                    ["Exports:MaximumRows"] = maximumRows.ToString()
                })
                .Build())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(identity),
                        Response = { Body = body }
                    }
                }
            };
            return new Fixture(connection, db, controller, body, team.Id);
        }

        private static SavingsEntry Entry(Team team, SavingReason reason, ProductGroup productGroup, AppUser user, string kvnr) => new()
        {
            Month = new DateTime(2026, 8, 1),
            Kvnr = kvnr,
            OldKvAmount = 100m,
            NewKvAmount = 40m,
            SavingAmount = 60m,
            Team = team,
            SavingReason = reason,
            ProductGroup = productGroup,
            CreatedByUser = user,
            CreatedAt = DateTime.UtcNow,
            TransmissionDate = DateTime.UtcNow,
            Version = 1
        };

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
            ResponseBody.Dispose();
        }
    }
}
