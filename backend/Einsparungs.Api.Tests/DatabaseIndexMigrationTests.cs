using Einsparungs.Api.Data;
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
}
