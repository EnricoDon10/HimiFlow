using Einsparungs.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        await SeedRolesAsync(db);
        await SeedTeamsAsync(db);
        await SeedSavingReasonsAsync(db);
        await SeedProductGroupsAsync(db);
        await SeedUsersAsync(db);
        await UpdateExistingDemoUsersAsync(db);
    }

    private static async Task SeedRolesAsync(AppDbContext db)
    {
        if (await db.Roles.AnyAsync())
        {
            return;
        }

        db.Roles.AddRange(
            new AppRole { Name = "Mitarbeiter" },
            new AppRole { Name = "Fuehrungskraft" },
            new AppRole { Name = "Admin" }
        );

        await db.SaveChangesAsync();
    }

    private static async Task SeedTeamsAsync(AppDbContext db)
    {
        if (await db.Teams.AnyAsync())
        {
            return;
        }

        db.Teams.AddRange(
            new Team { Code = "3410", Name = "Bochum 1", DisplayName = "Bochum 1 (3410)" },
            new Team { Code = "3420", Name = "Bochum 2", DisplayName = "Bochum 2 (3420)" },
            new Team { Code = "3430", Name = "Bochum 3", DisplayName = "Bochum 3 (3430)" },
            new Team { Code = "3440", Name = "Rüsselsheim", DisplayName = "Rüsselsheim (3440)" },
            new Team { Code = "3450", Name = "Lübeck", DisplayName = "Lübeck (3450)" }
        );

        await db.SaveChangesAsync();
    }

    private static async Task SeedSavingReasonsAsync(AppDbContext db)
    {
        if (await db.SavingReasons.AnyAsync())
        {
            return;
        }

        db.SavingReasons.AddRange(
            new SavingReason { Name = "vollständig keine med. Notwendigkeit" },
            new SavingReason { Name = "teilweise keine med. Notwendigkeit" },
            new SavingReason { Name = "Lagerversorgung" },
            new SavingReason { Name = "Kürzung auf Vertragspreis" },
            new SavingReason { Name = "Kürzung allgemein" },
            new SavingReason { Name = "Rabatt" },
            new SavingReason { Name = "Umversorgung auf anderes Himi" }
        );

        await db.SaveChangesAsync();
    }

    private static async Task SeedProductGroupsAsync(AppDbContext db)
    {
        if (await db.ProductGroups.AnyAsync())
        {
            return;
        }

        db.ProductGroups.AddRange(
            new ProductGroup { DisplayValue = "18.50.03.0xxx, Aktivrollstuhl", ImportedBy = "system" },
            new ProductGroup { DisplayValue = "17.10.xx.xxxx, Kompressionsartikel - nicht apparativ ARM", ImportedBy = "system" },
            new ProductGroup { DisplayValue = "31.03.xx.xxxx, Pflegehilfsmittel zur Körperpflege", ImportedBy = "system" },
            new ProductGroup { DisplayValue = "14.24.xx.xxxx, Inhalations- und Atemtherapiegeräte", ImportedBy = "system" },
            new ProductGroup { DisplayValue = "19.40.xx.xxxx, Krankenpflegeartikel", ImportedBy = "system" }
        );

        await db.SaveChangesAsync();
    }

    private static async Task SeedUsersAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var mitarbeiterRole = await db.Roles.SingleAsync(x => x.Name == "Mitarbeiter");
        var fuehrungskraftRole = await db.Roles.SingleAsync(x => x.Name == "Fuehrungskraft");
        var adminRole = await db.Roles.SingleAsync(x => x.Name == "Admin");

        var bochum1 = await db.Teams.SingleAsync(x => x.Code == "3410");
        var bochum2 = await db.Teams.SingleAsync(x => x.Code == "3420");
        var bochum3 = await db.Teams.SingleAsync(x => x.Code == "3430");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!");

        var enrico = new AppUser
        {
            UserName = "enrico.mancuso",
            DisplayName = "Enrico Mancuso",
            PasswordHash = passwordHash,
            TeamId = bochum1.Id
        };

        var daniel = new AppUser
        {
            UserName = "daniel.beck",
            DisplayName = "Daniel Beck",
            PasswordHash = passwordHash,
            TeamId = bochum2.Id
        };

        var marco = new AppUser
        {
            UserName = "marco.meyer",
            DisplayName = "Marco Meyer",
            PasswordHash = passwordHash,
            TeamId = bochum3.Id
        };

        var admin = new AppUser
        {
            UserName = "admin",
            DisplayName = "IT Admin Demo",
            PasswordHash = passwordHash
        };

        db.Users.AddRange(enrico, daniel, marco, admin);

        db.UserRoles.AddRange(
            new AppUserRole { AppUser = enrico, AppRole = mitarbeiterRole },
            new AppUserRole { AppUser = daniel, AppRole = mitarbeiterRole },
            new AppUserRole { AppUser = marco, AppRole = fuehrungskraftRole },
            new AppUserRole { AppUser = admin, AppRole = adminRole }
        );

        await db.SaveChangesAsync();
    }

    private static async Task UpdateExistingDemoUsersAsync(AppDbContext db)
    {
        await RenameDemoUserAsync(db, "mitarbeiter1", "enrico.mancuso", "Enrico Mancuso");
        await RenameDemoUserAsync(db, "mitarbeiter2", "daniel.beck", "Daniel Beck");
        await RenameDemoUserAsync(db, "teamleiter", "marco.meyer", "Marco Meyer");
    }

    private static async Task RenameDemoUserAsync(
        AppDbContext db,
        string oldUserName,
        string newUserName,
        string newDisplayName)
    {
        var existingTargetUser = await db.Users.FirstOrDefaultAsync(x => x.UserName == newUserName);

        if (existingTargetUser is not null)
        {
            existingTargetUser.DisplayName = newDisplayName;
            await db.SaveChangesAsync();
            return;
        }

        var oldDemoUser = await db.Users.FirstOrDefaultAsync(x => x.UserName == oldUserName);

        if (oldDemoUser is null)
        {
            return;
        }

        oldDemoUser.UserName = newUserName;
        oldDemoUser.DisplayName = newDisplayName;

        await db.SaveChangesAsync();
    }
}
