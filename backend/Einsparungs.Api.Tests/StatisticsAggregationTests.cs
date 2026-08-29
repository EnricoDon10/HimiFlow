using Einsparungs.Api.Controllers;
using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class StatisticsAggregationTests
{
    [TestMethod]
    public async Task Overview_PreservesExpectedBusinessValuesAndExcludesSoftDeletedEntries()
    {
        await using var fixture = await StatisticsFixture.CreateAsync();

        var result = await fixture.Controller.GetOverview();
        var overview = ((OkObjectResult)result.Result!).Value as StatisticsOverviewResponse;

        Assert.IsNotNull(overview);
        Assert.AreEqual(3, overview.EntryCount);
        Assert.AreEqual(39.30m, overview.TotalSavingAmount);
        Assert.AreEqual(13.10m, overview.AverageSavingAmount);
        Assert.AreEqual(20.20m, overview.HighestSavingAmount);
        Assert.AreEqual(9.00m, overview.LowestSavingAmount);
    }

    [TestMethod]
    public async Task GroupedStatistics_PreserveMonthlyAndMasterDataResults()
    {
        await using var fixture = await StatisticsFixture.CreateAsync();

        var monthlyResult = await fixture.Controller.GetMonthly();
        var monthly = ((OkObjectResult)monthlyResult.Result!).Value as List<MonthlySavingsStatisticResponse>;
        Assert.IsNotNull(monthly);
        Assert.AreEqual(2, monthly.Count);
        Assert.AreEqual("08.2026", monthly[0].MonthLabel);
        Assert.AreEqual(19.10m, monthly[0].TotalSavingAmount);
        Assert.AreEqual(9.55m, monthly[0].AverageSavingAmount);

        var teamResult = await fixture.Controller.GetByTeam();
        var teams = ((OkObjectResult)teamResult.Result!).Value as List<GroupedSavingsStatisticResponse>;
        Assert.IsNotNull(teams);
        Assert.AreEqual(2, teams.Count);
        Assert.AreEqual("Team 2", teams[0].GroupName);
        Assert.AreEqual(20.20m, teams[0].TotalSavingAmount);

        var reasonResult = await fixture.Controller.GetBySavingReason();
        var reasons = ((OkObjectResult)reasonResult.Result!).Value as List<GroupedSavingsStatisticResponse>;
        Assert.IsNotNull(reasons);
        Assert.AreEqual(2, reasons.Count);

        var productResult = await fixture.Controller.GetByProductGroup();
        var products = ((OkObjectResult)productResult.Result!).Value as List<GroupedSavingsStatisticResponse>;
        Assert.IsNotNull(products);
        Assert.AreEqual(2, products.Count);
    }

    private sealed class StatisticsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private StatisticsFixture(SqliteConnection connection, AppDbContext db)
        {
            this.connection = connection;
            Db = db;
            Controller = new StatisticsController(db);
        }

        public AppDbContext Db { get; }
        public StatisticsController Controller { get; }

        public static async Task<StatisticsFixture> CreateAsync()
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
            var productOne = new ProductGroup { DisplayValue = "PG 1" };
            var productTwo = new ProductGroup { DisplayValue = "PG 2" };
            var user = new AppUser
            {
                UserName = "employee",
                NormalizedUserName = "EMPLOYEE",
                DisplayName = "Mitarbeiter",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                Team = teamOne,
                MustChangePassword = false
            };

            db.AddRange(teamOne, teamTwo, reasonOne, reasonTwo, productOne, productTwo, user);
            await db.SaveChangesAsync();

            db.SavingsEntries.AddRange(
                Entry("A100000001", new DateTime(2026, 8, 1), 9.00m, teamOne, reasonOne, productOne, user),
                Entry("A100000002", new DateTime(2026, 8, 1), 10.10m, teamOne, reasonOne, productOne, user),
                Entry("A100000003", new DateTime(2026, 9, 1), 20.20m, teamTwo, reasonTwo, productTwo, user),
                Entry("A100000004", new DateTime(2026, 9, 1), 100m, teamTwo, reasonTwo, productTwo, user, isDeleted: true));
            await db.SaveChangesAsync();

            return new StatisticsFixture(connection, db);
        }

        private static SavingsEntry Entry(
            string kvnr,
            DateTime month,
            decimal savingAmount,
            Team team,
            SavingReason reason,
            ProductGroup productGroup,
            AppUser user,
            bool isDeleted = false)
        {
            return new SavingsEntry
            {
                Kvnr = kvnr,
                Month = month,
                OldKvAmount = 200m,
                NewKvAmount = 200m - savingAmount,
                SavingAmount = savingAmount,
                Team = team,
                SavingReason = reason,
                ProductGroup = productGroup,
                CreatedByUser = user,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? DateTime.UtcNow : null,
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
