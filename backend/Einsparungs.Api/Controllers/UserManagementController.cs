using System.Security.Claims;
using System.Data;
using System.Text.Json;
using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/user-management")]
[Authorize(Roles = ApplicationRoles.SystemAdmin)]
public sealed class UserManagementController : ControllerBase
{
    private readonly AppDbContext db;
    private readonly UserManager<AppUser> userManager;
    private readonly TemporaryPasswordGenerator temporaryPasswordGenerator;
    private readonly LicenseService licenseService;

    public UserManagementController(
        AppDbContext db,
        UserManager<AppUser> userManager,
        TemporaryPasswordGenerator temporaryPasswordGenerator,
        LicenseService licenseService)
    {
        this.db = db;
        this.userManager = userManager;
        this.temporaryPasswordGenerator = temporaryPasswordGenerator;
        this.licenseService = licenseService;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserManagementUserResponse>>> GetUsers()
    {
        var users = await UserQuery()
            .OrderBy(user => user.DisplayName)
            .ToListAsync();

        return Ok(users.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<CreateUserResponse>> CreateUser(CreateUserRequest request)
    {
        var errors = await ValidateCreateUserAsync(request);

        if (errors.Count > 0)
        {
            return BadRequest(ApiProblem.Validation(HttpContext, errors));
        }

        var roleName = request.RoleName.Trim();
        var role = await db.Roles.SingleAsync(item => item.Name == roleName);
        var temporaryPassword = temporaryPasswordGenerator.Generate();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var seatLimit = await licenseService.CheckActiveUserSlotAsync();
        if (!seatLimit.SlotAvailable)
        {
            return LicenseLimitConflict(seatLimit);
        }

        var user = new AppUser
        {
            UserName = request.UserName.Trim(),
            DisplayName = request.DisplayName.Trim(),
            TeamId = roleName == ApplicationRoles.SystemAdmin ? null : request.TeamId,
            IsActive = true,
            MustChangePassword = true,
            PasswordChangedAt = null
        };

        var creationResult = await userManager.CreateAsync(user, temporaryPassword);

        if (!creationResult.Succeeded)
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                creationResult.Errors.Select(error => error.Description)));
        }

        db.UserRoles.Add(new AppUserRole
        {
            AppUserId = user.Id,
            AppRoleId = role.Id
        });

        AddAdminAudit(user.Id, "Created", new
        {
            user.UserName,
            user.DisplayName,
            Role = roleName,
            user.TeamId
        });

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        var createdUser = await UserQuery().SingleAsync(item => item.Id == user.Id);

        return CreatedAtAction(
            nameof(GetUsers),
            new { id = user.Id },
            new CreateUserResponse(ToResponse(createdUser), temporaryPassword));
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword(Guid id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = await userManager.FindByIdAsync(id.ToString());

        if (user is null || user.IsDeleted)
        {
            return NotFound();
        }

        var temporaryPassword = temporaryPasswordGenerator.Generate();
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await userManager.ResetPasswordAsync(user, resetToken, temporaryPassword);

        if (!resetResult.Succeeded)
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                resetResult.Errors.Select(error => error.Description)));
        }

        user.MustChangePassword = true;
        user.PasswordChangedAt = null;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;

        var updateResult = await userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                updateResult.Errors.Select(error => error.Description)));
        }

        await userManager.UpdateSecurityStampAsync(user);

        AddAdminAudit(user.Id, "PasswordReset", new
        {
            user.UserName,
            user.DisplayName
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new ResetPasswordResponse(
            user.Id.ToString(),
            user.UserName ?? string.Empty,
            user.DisplayName,
            temporaryPassword));
    }

    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<UserManagementUserResponse>> ChangeRole(
        Guid id,
        ChangeUserRoleRequest request)
    {
        var roleName = request.RoleName.Trim();

        if (!ApplicationRoles.All.Contains(roleName, StringComparer.Ordinal))
        {
            return BadRequest(ApiProblem.Validation(HttpContext, ["Die ausgewählte Rolle existiert nicht."]));
        }

        var currentUserId = GetCurrentUserId();
        var user = await UserQuery().SingleOrDefaultAsync(item => item.Id == id);

        if (user is null)
        {
            return NotFound();
        }

        var currentRole = user.UserRoles.Single().AppRole.Name;

        if (currentRole == roleName)
        {
            return Ok(ToResponse(user));
        }

        if (id == currentUserId && currentRole == ApplicationRoles.SystemAdmin)
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                ["Der aktuell angemeldete System-Admin darf seine eigene Adminrolle nicht entfernen."]));
        }

        if (
            currentRole == ApplicationRoles.SystemAdmin &&
            await IsLastActiveSystemAdminAsync(id))
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                ["Die Rolle des letzten aktiven System-Admins darf nicht entfernt werden."]));
        }

        if (roleName != ApplicationRoles.SystemAdmin && user.TeamId is null)
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                ["Vor der Rollenänderung muss dem Benutzer ein Team zugeordnet sein."]));
        }

        if (roleName != ApplicationRoles.SystemAdmin && user.TeamId is not null &&
            !await db.Teams.AnyAsync(team => team.Id == user.TeamId.Value && team.IsActive))
        {
            return UserTeamInactive(user.TeamId);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var targetRole = await db.Roles.SingleAsync(role => role.Name == roleName);
        db.UserRoles.RemoveRange(user.UserRoles);
        db.UserRoles.Add(new AppUserRole { AppUserId = user.Id, AppRoleId = targetRole.Id });

        if (roleName == ApplicationRoles.SystemAdmin)
        {
            user.TeamId = null;
        }

        await db.SaveChangesAsync();
        await userManager.UpdateSecurityStampAsync(user);

        AddAdminAudit(user.Id, "RoleChanged", new
        {
            user.UserName,
            From = currentRole,
            To = roleName
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        var updatedUser = await UserQuery().SingleAsync(item => item.Id == id);
        return Ok(ToResponse(updatedUser));
    }

    [HttpPut("{id:guid}/team")]
    public async Task<ActionResult<UserManagementUserResponse>> ChangeTeam(
        Guid id,
        ChangeUserTeamRequest request)
    {
        var user = await UserQuery().SingleOrDefaultAsync(item => item.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        var roleName = user.UserRoles.Select(userRole => userRole.AppRole.Name).SingleOrDefault();
        if (roleName == ApplicationRoles.SystemAdmin)
        {
            return BadRequest(ApiProblem.Validation(HttpContext, ["Ein System-Admin benötigt keine Organisationseinheit."]));
        }

        var targetTeam = await db.Teams.SingleOrDefaultAsync(
            team => team.Id == request.TeamId && team.IsActive);
        if (targetTeam is null)
        {
            return UserTeamInactive(request.TeamId);
        }

        var oldTeam = user.TeamId.HasValue
            ? await db.Teams.AsNoTracking().SingleOrDefaultAsync(team => team.Id == user.TeamId.Value)
            : null;
        if (user.TeamId == targetTeam.Id)
        {
            return Ok(ToResponse(user));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        user.TeamId = targetTeam.Id;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                updateResult.Errors.Select(error => error.Description)));
        }

        await userManager.UpdateSecurityStampAsync(user);
        AddAdminAudit(user.Id, "TeamChanged", new
        {
            user.UserName,
            OldTeamId = oldTeam?.Id,
            OldTeam = oldTeam?.DisplayName,
            NewTeamId = targetTeam.Id,
            NewTeam = targetTeam.DisplayName
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        var updatedUser = await UserQuery().SingleAsync(item => item.Id == id);
        return Ok(ToResponse(updatedUser));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateUser(Guid id)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId == id)
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                ["Der aktuell angemeldete System-Admin kann sich nicht selbst deaktivieren."]));
        }

        var user = await UserQuery().SingleOrDefaultAsync(item => item.Id == id);

        if (user is null)
        {
            return NotFound();
        }

        if (await IsLastActiveSystemAdminAsync(id))
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                ["Der letzte aktive System-Admin darf nicht deaktiviert werden."]));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        user.IsActive = false;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                updateResult.Errors.Select(error => error.Description)));
        }
        await userManager.UpdateSecurityStampAsync(user);

        AddAdminAudit(user.Id, "Deactivated", new { user.UserName, user.DisplayName });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> ActivateUser(Guid id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = await userManager.FindByIdAsync(id.ToString());

        if (user is null || user.IsDeleted)
        {
            return NotFound();
        }

        if (!user.IsActive)
        {
            var seatLimit = await licenseService.CheckActiveUserSlotAsync();
            if (!seatLimit.SlotAvailable)
            {
                return LicenseLimitConflict(seatLimit);
            }

            var roleName = await db.UserRoles
                .Where(userRole => userRole.AppUserId == user.Id)
                .Select(userRole => userRole.AppRole.Name)
                .SingleOrDefaultAsync();
            if (roleName != ApplicationRoles.SystemAdmin)
            {
                var team = user.TeamId.HasValue
                    ? await db.Teams.SingleOrDefaultAsync(item => item.Id == user.TeamId.Value)
                    : null;
                if (team is null || !team.IsActive)
                {
                    return UserTeamInactive(user.TeamId);
                }
            }
        }

        user.IsActive = true;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                updateResult.Errors.Select(error => error.Description)));
        }
        await userManager.UpdateSecurityStampAsync(user);

        AddAdminAudit(user.Id, "Activated", new { user.UserName, user.DisplayName });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId == id)
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                ["Der aktuell angemeldete System-Admin kann sich nicht selbst löschen."]));
        }

        var user = await UserQuery().SingleOrDefaultAsync(item => item.Id == id);

        if (user is null)
        {
            return NotFound();
        }

        if (await IsLastActiveSystemAdminAsync(id))
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                ["Der letzte aktive System-Admin darf nicht gelöscht werden."]));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var deletionStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        user.IsActive = false;
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.UserName = $"{user.UserName}.deleted.{deletionStamp}";

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return BadRequest(ApiProblem.Validation(
                HttpContext,
                updateResult.Errors.Select(error => error.Description)));
        }
        await userManager.UpdateSecurityStampAsync(user);

        AddAdminAudit(user.Id, "Deleted", new { user.UserName, user.DisplayName });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return NoContent();
    }

    private IQueryable<AppUser> UserQuery()
    {
        return db.Users
            .Where(user => !user.IsDeleted)
            .Include(user => user.Team)
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.AppRole);
    }

    private async Task<List<string>> ValidateCreateUserAsync(CreateUserRequest request)
    {
        var errors = new List<string>();
        var roleName = request.RoleName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            errors.Add("Benutzername ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors.Add("Anzeigename ist erforderlich.");
        }

        if (!ApplicationRoles.All.Contains(roleName, StringComparer.Ordinal))
        {
            errors.Add("Die ausgewählte Rolle existiert nicht.");
        }

        if (roleName != ApplicationRoles.SystemAdmin)
        {
            if (request.TeamId is null)
            {
                errors.Add("Team ist für diese Rolle erforderlich.");
            }
            else if (!await db.Teams.AnyAsync(team => team.Id == request.TeamId.Value && team.IsActive))
            {
                errors.Add("Die ausgewählte Organisationseinheit existiert nicht oder ist deaktiviert.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            var existingUser = await userManager.FindByNameAsync(request.UserName.Trim());

            if (existingUser is not null && !existingUser.IsDeleted)
            {
                errors.Add("Benutzername ist bereits vergeben.");
            }
        }

        return errors;
    }

    private async Task<bool> IsLastActiveSystemAdminAsync(Guid userId)
    {
        var userIsSystemAdmin = await db.UserRoles.AnyAsync(userRole =>
            userRole.AppUserId == userId &&
            userRole.AppRole.Name == ApplicationRoles.SystemAdmin);

        if (!userIsSystemAdmin)
        {
            return false;
        }

        var activeSystemAdminCount = await db.Users.CountAsync(user =>
            user.IsActive &&
            !user.IsDeleted &&
            user.UserRoles.Any(userRole => userRole.AppRole.Name == ApplicationRoles.SystemAdmin));

        return activeSystemAdminCount <= 1;
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                    User.FindFirstValue(SecurityClaims.UserId);

        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static UserManagementUserResponse ToResponse(AppUser user)
    {
        var roleName = user.UserRoles
            .Select(userRole => userRole.AppRole.Name)
            .SingleOrDefault() ?? "-";

        return new UserManagementUserResponse(
            user.Id.ToString(),
            user.UserName ?? string.Empty,
            user.DisplayName,
            roleName,
            user.TeamId,
            user.Team?.DisplayName,
            user.IsActive,
            user.MustChangePassword);
    }

    private ConflictObjectResult LicenseLimitConflict(LicenseSeatLimitResult seatLimit)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Lizenzlimit erreicht",
            Detail = "Die maximale Anzahl aktiver Benutzer ist erreicht. Deaktivieren Sie zunächst einen nicht benötigten Benutzer.",
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = "LICENSE_MAX_USERS_REACHED";
        problem.Extensions["maxUsers"] = seatLimit.MaxUsers;
        problem.Extensions["activeUsers"] = seatLimit.ActiveUsers;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return Conflict(problem);
    }

    private ConflictObjectResult UserTeamInactive(int? teamId)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Organisationseinheit ist deaktiviert",
            Detail = "Der Benutzer kann erst aktiviert oder zugeordnet werden, wenn eine aktive Organisationseinheit ausgewählt wurde.",
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = "USER_TEAM_INACTIVE";
        problem.Extensions["teamId"] = teamId;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return Conflict(problem);
    }

    private void AddAdminAudit(Guid entityId, string action, object details)
    {
        var changedByUserId = GetCurrentUserId();
        if (changedByUserId is null)
        {
            return;
        }

        db.AuditLogs.Add(new AuditLog
        {
            EntityName = "User",
            EntityId = entityId.ToString(),
            Action = action,
            ChangedByUserId = changedByUserId.Value,
            ChangedAt = DateTime.UtcNow,
            NewValuesJson = JsonSerializer.Serialize(details),
            ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        });
    }
}

public sealed record CreateUserRequest(
    string UserName,
    string DisplayName,
    string RoleName,
    int? TeamId);

public sealed record ChangeUserRoleRequest(string RoleName);

public sealed record ChangeUserTeamRequest(int TeamId);

public sealed record UserManagementUserResponse(
    string Id,
    string UserName,
    string DisplayName,
    string RoleName,
    int? TeamId,
    string? TeamDisplayName,
    bool IsActive,
    bool MustChangePassword);

public sealed record CreateUserResponse(
    UserManagementUserResponse User,
    string TemporaryPassword);

public sealed record ResetPasswordResponse(
    string Id,
    string UserName,
    string DisplayName,
    string TemporaryPassword);
