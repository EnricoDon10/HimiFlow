using Einsparungs.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Data;

/// <summary>
/// Validates data that would make a schema upgrade unsafe. Preflight never
/// changes data; it only blocks an upgrade with an actionable operator message.
/// </summary>
public static class DatabasePreflight
{
    public static async Task ValidateBeforeMigrationAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return;
        }

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
        if (!appliedMigrations.Any())
        {
            // A fresh database has no UserRoles table yet. The migration will
            // create it and the unique index in the same controlled operation.
            return;
        }

        var roleAssignments = await db.UserRoles
            .AsNoTracking()
            .Include(item => item.AppUser)
            .Include(item => item.AppRole)
            .ToListAsync(cancellationToken);

        var duplicates = roleAssignments
            .GroupBy(item => item.AppUserId)
            .Where(group => group.Count() > 1)
            .Select(group =>
            {
                var first = group.First();
                var userLabel = string.IsNullOrWhiteSpace(first.AppUser.DisplayName)
                    ? first.AppUser.UserName ?? first.AppUserId.ToString()
                    : first.AppUser.DisplayName;
                var roles = string.Join(", ", group
                    .Select(item => item.AppRole.Name)
                    .OrderBy(name => name, StringComparer.Ordinal));
                return $"{userLabel} ({first.AppUser.UserName ?? "ohne Benutzername"}, ID {first.AppUserId}) – Rollen: {roles}";
            })
            .ToArray();

        if (duplicates.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Datenbank-Preflight abgebrochen: Die Ein-Rolle-pro-Benutzer-Regel kann nicht sicher angewendet werden. " +
            "Betroffene Benutzer: " + string.Join("; ", duplicates) + ". " +
            "Es wurden keine Daten gelöscht. Bitte die Rollen der betroffenen Benutzer manuell bereinigen " +
            "und das Upgrade anschließend erneut starten.");
    }
}
