using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class AuditRetentionServiceTests
{
    [TestMethod]
    [DataRow(false, 30)]
    [DataRow(true, 0)]
    public async Task Cleanup_DoesNothingWhenDisabledOrRetentionIsZero(bool enabled, int retentionDays)
    {
        await using var fixture = await RetentionFixture.CreateAsync();
        var service = fixture.CreateService(enabled, retentionDays);

        var deleted = await service.CleanupAsync();

        Assert.AreEqual(0, deleted);
        Assert.AreEqual(2, await fixture.Db.AuditLogs.CountAsync());
    }

    [TestMethod]
    public async Task Cleanup_DeletesOnlyExpiredAuditLogs()
    {
        await using var fixture = await RetentionFixture.CreateAsync();
        var service = fixture.CreateService(true, 30);

        var deleted = await service.CleanupAsync();

        Assert.AreEqual(1, deleted);
        var remaining = await fixture.Db.AuditLogs.SingleAsync();
        Assert.AreEqual("Recent", remaining.Action);
    }

    private sealed class RetentionFixture : IAsyncDisposable
    {
        private static readonly DateTimeOffset Now = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
        private readonly SqliteConnection connection;

        private RetentionFixture(SqliteConnection connection, AppDbContext db)
        {
            this.connection = connection;
            Db = db;
        }

        public AppDbContext Db { get; }

        public static async Task<RetentionFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var user = new AppUser
            {
                UserName = "auditor",
                NormalizedUserName = "AUDITOR",
                DisplayName = "Audit User",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.AuditLogs.AddRange(
                Log("Expired", Now.UtcDateTime.AddDays(-31), user.Id),
                Log("Recent", Now.UtcDateTime.AddDays(-29), user.Id));
            await db.SaveChangesAsync();
            return new RetentionFixture(connection, db);
        }

        public AuditRetentionService CreateService(bool enabled, int retentionDays)
        {
            return new AuditRetentionService(
                Db,
                Options.Create(new AuditRetentionOptions
                {
                    CleanupEnabled = enabled,
                    RetentionDays = retentionDays,
                    BatchSize = 1
                }),
                new FixedTimeProvider(Now),
                NullLogger<AuditRetentionService>.Instance);
        }

        private static AuditLog Log(string action, DateTime changedAt, Guid userId) => new()
        {
            EntityName = "TestEntity",
            EntityId = Guid.NewGuid().ToString(),
            Action = action,
            ChangedAt = changedAt,
            ChangedByUserId = userId
        };

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
