using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Einsparungs.Api.Controllers;
using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Einsparungs.Api.Security;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class SavingsPaginationTests
{
    [TestMethod]
    public async Task GetAllSavings_ReturnsRequestedPageAndTotals()
    {
        await using var fixture = await PaginationFixture.CreateAsync();

        var result = await fixture.Controller.GetAllSavings(new SavingsListQuery
        {
            Page = 2,
            PageSize = 2
        });

        var page = ((OkObjectResult)result.Result!).Value as PagedResponse<SavingsEntryResponse>;
        Assert.IsNotNull(page);
        Assert.AreEqual(2, page.Page);
        Assert.AreEqual(2, page.PageSize);
        Assert.AreEqual(4, page.TotalCount);
        Assert.AreEqual(2, page.TotalPages);
        Assert.AreEqual(2, page.Items.Count);
    }

    [TestMethod]
    public async Task GetAllSavings_AppliesCombinedFiltersAndExcludesDeletedEntries()
    {
        await using var fixture = await PaginationFixture.CreateAsync();

        var result = await fixture.Controller.GetAllSavings(new SavingsListQuery
        {
            Page = 1,
            PageSize = 50,
            Month = new DateTime(2026, 8, 19),
            TeamId = fixture.TeamOneId,
            SavingReasonId = fixture.ReasonOneId
        });

        var page = ((OkObjectResult)result.Result!).Value as PagedResponse<SavingsEntryResponse>;
        Assert.IsNotNull(page);
        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual(1, page.Items.Count);
        Assert.AreEqual("A100000001", page.Items[0].Kvnr);
    }

    [TestMethod]
    public async Task GetMySavings_ReturnsRequestedPageAndTotals()
    {
        await using var fixture = await PaginationFixture.CreateAsync();

        var result = await fixture.Controller.GetMySavings(new SavingsListQuery
        {
            Page = 2,
            PageSize = 2
        });

        var page = ((OkObjectResult)result.Result!).Value as PagedResponse<SavingsEntryResponse>;
        Assert.IsNotNull(page);
        Assert.AreEqual(2, page.Page);
        Assert.AreEqual(2, page.PageSize);
        Assert.AreEqual(4, page.TotalCount);
        Assert.AreEqual(2, page.TotalPages);
        Assert.AreEqual(2, page.Items.Count);
    }

    [TestMethod]
    public async Task GetAllSavings_WithPageSizeZeroIsRejected()
    {
        await using var fixture = await PaginationFixture.CreateAsync();
        var result = await fixture.Controller.GetAllSavings(new SavingsListQuery
        {
            Page = 1,
            PageSize = 0
        });

        Assert.IsInstanceOfType(result.Result, typeof(BadRequestObjectResult));
    }

    [TestMethod]
    public void SavingsListQuery_RejectsPageSizeAboveMaximum()
    {
        var request = new SavingsListQuery { PageSize = 101 };
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.IsFalse(isValid);
        Assert.IsTrue(validationResults.Any(result =>
            result.MemberNames.Contains(nameof(SavingsListQuery.PageSize))));
    }

    private sealed class PaginationFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private PaginationFixture(
            SqliteConnection connection,
            AppDbContext db,
            SavingsController controller,
            int teamOneId,
            int reasonOneId)
        {
            this.connection = connection;
            Db = db;
            Controller = controller;
            TeamOneId = teamOneId;
            ReasonOneId = reasonOneId;
        }

        public AppDbContext Db { get; }
        public SavingsController Controller { get; }
        public int TeamOneId { get; }
        public int ReasonOneId { get; }

        public static async Task<PaginationFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var teamOne = new Team { Code = "T01", Name = "Team 1", DisplayName = "Team 1" };
            var teamTwo = new Team { Code = "T02", Name = "Team 2", DisplayName = "Team 2" };
            var reasonOne = new SavingReason { Name = "Grund 1" };
            var reasonTwo = new SavingReason { Name = "Grund 2" };
            var productGroup = new ProductGroup { DisplayValue = "PG 01" };
            var user = new AppUser
            {
                UserName = "fachadmin",
                NormalizedUserName = "FACHADMIN",
                DisplayName = "Fach Admin",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                Team = teamOne,
                MustChangePassword = false
            };

            db.AddRange(teamOne, teamTwo, reasonOne, reasonTwo, productGroup, user);
            await db.SaveChangesAsync();

            var createdAt = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
            db.SavingsEntries.AddRange(
                Entry("A100000001", new DateTime(2026, 8, 1), teamOne, reasonOne, productGroup, user, createdAt),
                Entry("A100000002", new DateTime(2026, 8, 1), teamOne, reasonOne, productGroup, user, createdAt.AddMinutes(-1), isDeleted: true),
                Entry("A100000003", new DateTime(2026, 8, 1), teamTwo, reasonOne, productGroup, user, createdAt.AddMinutes(-2)),
                Entry("A100000004", new DateTime(2026, 7, 1), teamOne, reasonTwo, productGroup, user, createdAt.AddMinutes(-3)),
                Entry("A100000005", new DateTime(2026, 6, 1), teamOne, reasonOne, productGroup, user, createdAt.AddMinutes(-4)));
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
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
                }
            };

            return new PaginationFixture(
                connection,
                db,
                controller,
                teamOne.Id,
                reasonOne.Id);
        }

        private static SavingsEntry Entry(
            string kvnr,
            DateTime month,
            Team team,
            SavingReason reason,
            ProductGroup productGroup,
            AppUser user,
            DateTime createdAt,
            bool isDeleted = false)
        {
            return new SavingsEntry
            {
                Kvnr = kvnr,
                Month = month,
                OldKvAmount = 100m,
                NewKvAmount = 50m,
                SavingAmount = 50m,
                Team = team,
                SavingReason = reason,
                ProductGroup = productGroup,
                CreatedByUser = user,
                CreatedAt = createdAt,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? createdAt.AddMinutes(1) : null,
                DeletedByUser = isDeleted ? user : null
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
