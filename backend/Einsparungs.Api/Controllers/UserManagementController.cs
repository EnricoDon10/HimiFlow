using System.Security.Claims;
using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/user-management")]
[Authorize(Roles = "Admin")]
public sealed class UserManagementController : ControllerBase
{
    private const string DefaultResetPassword = "Demo123!";

    private readonly AppDbContext db;

    public UserManagementController(AppDbContext db)
    {
        this.db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserManagementUserResponse>>> GetUsers()
    {
        var users = await db.Users
            .Where(user => !user.IsDeleted)
            .Include(user => user.Team)
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.AppRole)
            .OrderBy(user => user.DisplayName)
            .ToListAsync();

        return Ok(users.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<UserManagementUserResponse>> CreateUser(CreateUserRequest request)
    {
        var errors = await ValidateCreateUserAsync(request);

        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        var role = await db.Roles.SingleAsync(item => item.Name == request.RoleName.Trim());

        var user = new AppUser
        {
            UserName = request.UserName.Trim(),
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password.Trim()),
            TeamId = request.TeamId
        };

        db.Users.Add(user);
        db.UserRoles.Add(new AppUserRole
        {
            AppUser = user,
            AppRole = role
        });

        await db.SaveChangesAsync();

        var createdUser = await db.Users
            .Include(item => item.Team)
            .Include(item => item.UserRoles)
                .ThenInclude(item => item.AppRole)
            .SingleAsync(item => item.Id == user.Id);

        return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, ToResponse(createdUser));
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword(Guid id)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == id);

        if (user is null)
        {
            return NotFound();
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultResetPassword);

        await db.SaveChangesAsync();

        return Ok(new ResetPasswordResponse(
            user.Id.ToString(),
            user.UserName,
            user.DisplayName,
            DefaultResetPassword
        ));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId == id)
        {
            return BadRequest(new
            {
                errors = new[] { "Der aktuell angemeldete Admin kann sich nicht selbst löschen." }
            });
        }

        var user = await db.Users
            .Include(item => item.UserRoles)
                .ThenInclude(item => item.AppRole)
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted);

        if (user is null)
        {
            return NotFound();
        }

        var isAdmin = user.UserRoles.Any(item => item.AppRole.Name == "Admin");

        if (isAdmin)
        {
            var activeAdminCount = await db.Users
                .CountAsync(item =>
                    !item.IsDeleted &&
                    item.UserRoles.Any(userRole => userRole.AppRole.Name == "Admin"));

            if (activeAdminCount <= 1)
            {
                return BadRequest(new
                {
                    errors = new[] { "Der letzte Admin-Benutzer darf nicht gelöscht werden." }
                });
            }
        }

        var deletionStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.UserName = $"{user.UserName}.deleted.{deletionStamp}";

        await db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<List<string>> ValidateCreateUserAsync(CreateUserRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            errors.Add("Benutzername ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors.Add("Anzeigename ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors.Add("Passwort ist erforderlich.");
        }
        else if (request.Password.Trim().Length < 6)
        {
            errors.Add("Passwort muss mindestens 6 Zeichen lang sein.");
        }

        if (string.IsNullOrWhiteSpace(request.RoleName))
        {
            errors.Add("Rolle ist erforderlich.");
        }
        else
        {
            var roleExists = await db.Roles.AnyAsync(role => role.Name == request.RoleName.Trim());

            if (!roleExists)
            {
                errors.Add("Die ausgewählte Rolle existiert nicht.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            var normalizedUserName = request.UserName.Trim().ToLower();

            var userNameExists = await db.Users
                .AnyAsync(user => !user.IsDeleted && user.UserName.ToLower() == normalizedUserName);

            if (userNameExists)
            {
                errors.Add("Benutzername ist bereits vergeben.");
            }
        }

        if (request.TeamId is not null)
        {
            var teamExists = await db.Teams.AnyAsync(team => team.Id == request.TeamId.Value);

            if (!teamExists)
            {
                errors.Add("Das ausgewählte Team existiert nicht.");
            }
        }

        return errors;
    }

    private Guid? GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue("userId");

        if (Guid.TryParse(value, out var userId))
        {
            return userId;
        }

        return null;
    }

    private static UserManagementUserResponse ToResponse(AppUser user)
    {
        var roleName = user.UserRoles
            .Select(userRole => userRole.AppRole.Name)
            .FirstOrDefault() ?? "-";

        return new UserManagementUserResponse(
            user.Id.ToString(),
            user.UserName,
            user.DisplayName,
            roleName,
            user.TeamId,
            user.Team?.DisplayName
        );
    }
}

public sealed record CreateUserRequest(
    string UserName,
    string DisplayName,
    string Password,
    string RoleName,
    int? TeamId
);

public sealed record UserManagementUserResponse(
    string Id,
    string UserName,
    string DisplayName,
    string RoleName,
    int? TeamId,
    string? TeamDisplayName
);

public sealed record ResetPasswordResponse(
    string Id,
    string UserName,
    string DisplayName,
    string NewPassword
);

