using System.Diagnostics;
using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class SQLitePerformanceSmokeTests
{
    [TestMethod]
    [TestCategory("PerformanceSmoke")]
    public async Task TenThousandRowsSupportPaginationFiltersAndAggregation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var team = new Team { Code = "SMOKE", Name = "Smoke Team", DisplayName = "Smoke Team (SMOKE)" };
        var reason = new SavingReason { Name = "Smoke Reason" };
        var productGroup = new ProductGroup { DisplayValue = "Smoke Product" };
        var user = new AppUser
        {
            UserName = "performance-smoke",
            NormalizedUserName = "PERFORMANCE-SMOKE",
            DisplayName = "Performance Smoke",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            Team = team,
            IsActive = true
        };
        db.AddRange(team, reason, productGroup, user);
        await db.SaveChangesAsync();

        var entries = Enumerable.Range(0, 10_000).Select(index => new SavingsEntry
        {
            Id = DeterministicGuid(index),
            Month = new DateTime(2026, (index % 12) + 1, 1),
            Kvnr = $"A{index % 1_000_000_000:000000000}",
            OldKvAmount = 100m,
            NewKvAmount = 40m,
            SavingAmount = 60m,
            Team = team,
            SavingReason = reason,
            ProductGroup = productGroup,
            CreatedByUser = user,
            CreatedAt = DateTime.UtcNow.AddMinutes(-index),
            TransmissionDate = DateTime.UtcNow,
            Version = 1
        }).ToArray();
        db.SavingsEntries.AddRange(entries);
        await db.SaveChangesAsync();

        var stopwatch = Stopwatch.StartNew();
        var page = await db.SavingsEntries.AsNoTracking()
            .Where(entry => !entry.IsDeleted && entry.TeamId == team.Id)
            .OrderByDescending(entry => entry.Month)
            .ThenByDescending(entry => entry.CreatedAt)
            .Skip(500)
            .Take(100)
            .ToListAsync();
        var total = await db.SavingsEntries.AsNoTracking()
            .Where(entry => !entry.IsDeleted && entry.Month == new DateTime(2026, 8, 1))
            .SumAsync(entry => entry.SavingAmount);
        stopwatch.Stop();

        Assert.AreEqual(100, page.Count);
        Assert.AreEqual(49_980m, total);
        Console.WriteLine($"SQLite performance smoke: 10,000 rows; page/filter/aggregate query {stopwatch.ElapsedMilliseconds} ms.");
    }

    private static Guid DeterministicGuid(int index)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(index).CopyTo(bytes, 0);
        return new Guid(bytes);
    }
}
