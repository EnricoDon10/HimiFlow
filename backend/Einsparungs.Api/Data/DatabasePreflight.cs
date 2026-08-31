using System.Data;
using System.Data.Common;
using System.Globalization;
using Einsparungs.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Data;

/// <summary>
/// Validates data that would make a schema upgrade unsafe. Preflight never
/// changes data; it only blocks an upgrade with an actionable operator message.
/// The check deliberately uses the historical relational schema instead of
/// materializing the current Identity model before pending migrations run.
/// </summary>
public static class DatabasePreflight
{
    private const string UserRolesTable = "UserRoles";
    private const string UsersTable = "Users";

    public static async Task ValidateBeforeMigrationAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return;
        }

        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var isSqlite = IsSqlite(db);
            if (!await TableExistsAsync(connection, UserRolesTable, isSqlite, cancellationToken))
            {
                // Fresh databases and historical databases before UserRoles
                // have nothing to validate yet. The migration can proceed.
                return;
            }

            var duplicates = await ReadDuplicateAssignmentsAsync(
                connection,
                isSqlite,
                cancellationToken);
            if (duplicates.Count == 0)
            {
                return;
            }

            var userLabels = await ReadUserLabelsAsync(
                connection,
                isSqlite,
                duplicates,
                cancellationToken);

            var details = duplicates
                .Select(duplicate => FormatDuplicate(duplicate, userLabels))
                .ToArray();

            throw new InvalidOperationException(
                "Datenbank-Preflight abgebrochen: Die Ein-Rolle-pro-Benutzer-Regel kann nicht sicher angewendet werden. " +
                "Betroffene Benutzer: " + string.Join("; ", details) + ". " +
                "Es wurden keine Daten gelöscht. Bitte die Rollen der betroffenen Benutzer manuell bereinigen " +
                "und das Upgrade anschließend erneut starten.");
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static bool IsSqlite(AppDbContext db) =>
        db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string tableName,
        bool isSqlite,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = isSqlite
            ? "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @tableName);"
            : "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @tableName) THEN 1 ELSE 0 END;";
        AddParameter(command, "@tableName", tableName);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<IReadOnlyList<RoleDuplicate>> ReadDuplicateAssignmentsAsync(
        DbConnection connection,
        bool isSqlite,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = isSqlite
            ? "SELECT AppUserId, COUNT(*) FROM \"UserRoles\" GROUP BY AppUserId HAVING COUNT(*) > 1;"
            : "SELECT [AppUserId], COUNT(*) FROM [UserRoles] GROUP BY [AppUserId] HAVING COUNT(*) > 1;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var duplicates = new List<RoleDuplicate>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var rawUserId = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty;
            var count = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
            duplicates.Add(new RoleDuplicate(rawUserId, count));
        }

        return duplicates;
    }

    private static async Task<IReadOnlyDictionary<string, UserLabel>> ReadUserLabelsAsync(
        DbConnection connection,
        bool isSqlite,
        IReadOnlyList<RoleDuplicate> duplicates,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, UsersTable, isSqlite, cancellationToken))
        {
            return new Dictionary<string, UserLabel>(StringComparer.OrdinalIgnoreCase);
        }

        var columns = await ReadColumnsAsync(connection, UsersTable, isSqlite, cancellationToken);
        var optionalColumns = new[] { "DisplayName", "UserName" }
            .Where(columns.Contains)
            .ToArray();
        if (optionalColumns.Length == 0 || !columns.Contains("Id"))
        {
            return new Dictionary<string, UserLabel>(StringComparer.OrdinalIgnoreCase);
        }

        var selectedColumns = new[] { "Id" }.Concat(optionalColumns).ToArray();
        var quotedColumns = string.Join(", ", selectedColumns.Select(column => QuoteIdentifier(column, isSqlite)));
        var table = QuoteIdentifier(UsersTable, isSqlite);
        var idColumn = QuoteIdentifier("Id", isSqlite);
        var labels = new Dictionary<string, UserLabel>(StringComparer.OrdinalIgnoreCase);

        foreach (var duplicate in duplicates)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {quotedColumns} FROM {table} WHERE {idColumn} = @userId;";
            AddParameter(command, "@userId", ConvertUserIdForProvider(duplicate.RawUserId, isSqlite));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                continue;
            }

            var displayName = ReadOptionalString(reader, selectedColumns, "DisplayName");
            var userName = ReadOptionalString(reader, selectedColumns, "UserName");
            labels[duplicate.RawUserId] = new UserLabel(displayName, userName);
        }

        return labels;
    }

    private static async Task<HashSet<string>> ReadColumnsAsync(
        DbConnection connection,
        string tableName,
        bool isSqlite,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = isSqlite
            ? "PRAGMA table_info(\"Users\");"
            : "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tableName;";
        if (!isSqlite)
        {
            AddParameter(command, "@tableName", tableName);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var nameIndex = isSqlite ? 1 : 0;
            columns.Add(Convert.ToString(reader.GetValue(nameIndex), CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return columns;
    }

    private static string FormatDuplicate(
        RoleDuplicate duplicate,
        IReadOnlyDictionary<string, UserLabel> userLabels)
    {
        if (!userLabels.TryGetValue(duplicate.RawUserId, out var label))
        {
            return $"Benutzer-ID {duplicate.RawUserId} ({duplicate.Count} Rollenzuordnungen)";
        }

        var visibleLabel = !string.IsNullOrWhiteSpace(label.DisplayName)
            ? label.DisplayName
            : !string.IsNullOrWhiteSpace(label.UserName)
                ? label.UserName
                : null;
        return visibleLabel is null
            ? $"Benutzer-ID {duplicate.RawUserId} ({duplicate.Count} Rollenzuordnungen)"
            : $"{visibleLabel} ({label.UserName ?? "ohne Benutzernamen"}, ID {duplicate.RawUserId}; {duplicate.Count} Rollenzuordnungen)";
    }

    private static string? ReadOptionalString(
        DbDataReader reader,
        IReadOnlyList<string> selectedColumns,
        string column)
    {
        var index = -1;
        for (var i = 0; i < selectedColumns.Count; i++)
        {
            if (string.Equals(selectedColumns[i], column, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        return index >= 0 && !reader.IsDBNull(index)
            ? Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture)
            : null;
    }

    private static object ConvertUserIdForProvider(string rawUserId, bool isSqlite) =>
        isSqlite || !Guid.TryParse(rawUserId, out var userId)
            ? rawUserId
            : userId;

    private static string QuoteIdentifier(string identifier, bool isSqlite) =>
        isSqlite
            ? $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record RoleDuplicate(string RawUserId, int Count);

    private sealed record UserLabel(string? DisplayName, string? UserName);
}
