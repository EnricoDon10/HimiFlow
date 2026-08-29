using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Einsparungs.Api.Data;

/// <summary>
/// Disambiguates SQLite design-time operations from the derived SQL Server context.
/// Runtime migrations continue to use the configured application connection.
/// </summary>
public sealed class SqliteAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Data Source=einsparungen-design-time.db";
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
