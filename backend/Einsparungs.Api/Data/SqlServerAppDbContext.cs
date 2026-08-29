using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Einsparungs.Api.Data;

/// <summary>
/// Uses the shared HimiFlow model with an independent SQL Server migration history.
/// Runtime services continue to depend on <see cref="AppDbContext"/>.
/// </summary>
public sealed class SqlServerAppDbContext : AppDbContext
{
    public SqlServerAppDbContext(DbContextOptions<SqlServerAppDbContext> options)
        : base(options)
    {
    }
}

/// <summary>
/// Design-time only. The placeholder connection is never opened while migrations
/// are generated and contains no customer credentials.
/// </summary>
public sealed class SqlServerAppDbContextFactory : IDesignTimeDbContextFactory<SqlServerAppDbContext>
{
    public SqlServerAppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SqlServerAppDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=HimiFlowDesignTime;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=1")
            .Options;

        return new SqlServerAppDbContext(options);
    }
}
