using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class DatabaseIndexMigrationTests
{
    [TestMethod]
    public async Task SqliteMigrations_CreateTheDocumentedSavingsIndexes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);

        await db.Database.MigrateAsync();

        var actualIndexes = new HashSet<string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA index_list('SavingsEntries');";
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            actualIndexes.Add(reader.GetString(1));
        }

        CollectionAssert.IsSubsetOf(
            new[]
            {
                "IX_SavingsEntries_ActiveMonthCreatedAt",
                "IX_SavingsEntries_UserActiveMonthCreatedAt",
                "IX_SavingsEntries_TeamActiveMonth",
                "IX_SavingsEntries_ReasonActiveMonth",
                "IX_SavingsEntries_ProductGroupActiveMonth"
            },
            actualIndexes.ToArray());
    }

    [TestMethod]
    public async Task MigrationPreflight_AllowsCleanDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);

        await DatabasePreflight.ValidateBeforeMigrationAsync(db);
        await db.Database.MigrateAsync();
    }

    [TestMethod]
    public async Task MigrationPreflight_AbortsDuplicateRolesWithoutDeletingAssignments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "DROP INDEX IX_UserRoles_AppUserId_Unique;";
            await command.ExecuteNonQueryAsync();
        }

        var user = new AppUser
        {
            UserName = "duplicate-role-user",
            NormalizedUserName = "DUPLICATE-ROLE-USER",
            DisplayName = "Änderung Testbenutzer",
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        var employee = new AppRole { Name = ApplicationRoles.Mitarbeiter };
        var admin = new AppRole { Name = ApplicationRoles.FachAdmin };
        db.AddRange(user, employee, admin);
        await db.SaveChangesAsync();
        db.UserRoles.AddRange(
            new AppUserRole { AppUserId = user.Id, AppRoleId = employee.Id },
            new AppUserRole { AppUserId = user.Id, AppRoleId = admin.Id });
        await db.SaveChangesAsync();

        var beforeCount = await db.UserRoles.CountAsync(item => item.AppUserId == user.Id);
        InvalidOperationException? exception = null;
        try
        {
            await DatabasePreflight.ValidateBeforeMigrationAsync(db);
        }
        catch (InvalidOperationException caught)
        {
            exception = caught;
        }
        Assert.IsNotNull(exception);
        var afterCount = await db.UserRoles.CountAsync(item => item.AppUserId == user.Id);

        Assert.AreEqual(2, beforeCount);
        Assert.AreEqual(beforeCount, afterCount);
        StringAssert.Contains(exception!.Message, "Änderung Testbenutzer");
        StringAssert.Contains(exception.Message, "keine Daten gelöscht");
        StringAssert.Contains(exception.Message, "manuell bereinigen");
    }

    [TestMethod]
    public async Task SqliteSchema_AllowsAtMostOneRolePerUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);

        await db.Database.MigrateAsync();
        var user = new AppUser
        {
            UserName = "role-constraint-test",
            NormalizedUserName = "ROLE-CONSTRAINT-TEST",
            DisplayName = "Rollen-Constraint Test",
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        var employee = new AppRole { Name = ApplicationRoles.Mitarbeiter };
        var admin = new AppRole { Name = ApplicationRoles.FachAdmin };
        db.AddRange(user, employee, admin);
        await db.SaveChangesAsync();

        db.UserRoles.AddRange(
            new AppUserRole { AppUserId = user.Id, AppRoleId = employee.Id },
            new AppUserRole { AppUserId = user.Id, AppRoleId = admin.Id });

        var threw = false;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            threw = true;
        }

        Assert.IsTrue(threw, "Die Datenbank muss eine zweite Rolle für denselben Benutzer ablehnen.");
    }
}
