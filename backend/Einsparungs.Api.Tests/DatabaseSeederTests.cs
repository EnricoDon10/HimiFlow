using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class DatabaseSeederTests
{
    [TestMethod]
    public async Task SeedReferenceDataAsync_DoesNotOverwriteAnExistingPassword()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await DatabaseSeeder.SeedReferenceDataAsync(db);

        var role = await db.Roles.SingleAsync(item => item.Name == "SystemAdmin");
        var userSelectedPasswordHash = BCrypt.Net.BCrypt.HashPassword(
            "A-unique-user-selected-password-2026!");
        var user = new AppUser
        {
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            DisplayName = "IT Admin",
            PasswordHash = userSelectedPasswordHash,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };

        db.Users.Add(user);
        db.UserRoles.Add(new AppUserRole { AppUser = user, AppRole = role });
        await db.SaveChangesAsync();

        await DatabaseSeeder.SeedReferenceDataAsync(db);

        var reloadedUser = await db.Users
            .AsNoTracking()
            .SingleAsync(item => item.UserName == "admin");

        Assert.AreEqual(userSelectedPasswordHash, reloadedUser.PasswordHash);
    }

    [TestMethod]
    public async Task SeedReferenceDataAsync_IsIdempotentForReferenceData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await DatabaseSeeder.SeedReferenceDataAsync(db);

        var expectedCounts = new
        {
            Roles = await db.Roles.CountAsync(),
            Teams = await db.Teams.CountAsync(),
            SavingReasons = await db.SavingReasons.CountAsync(),
            ProductGroups = await db.ProductGroups.CountAsync()
        };

        await DatabaseSeeder.SeedReferenceDataAsync(db);

        Assert.AreEqual(expectedCounts.Roles, await db.Roles.CountAsync());
        Assert.AreEqual(expectedCounts.Teams, await db.Teams.CountAsync());
        Assert.AreEqual(expectedCounts.SavingReasons, await db.SavingReasons.CountAsync());
        Assert.AreEqual(expectedCounts.ProductGroups, await db.ProductGroups.CountAsync());
    }
}
