using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/master-data")]
[Authorize]
public sealed class MasterDataController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext db;

    public MasterDataController(AppDbContext db)
    {
        this.db = db;
    }

    [HttpGet("teams")]
    [Authorize(Roles = ApplicationRoles.Mitarbeiter + "," + ApplicationRoles.FachAdmin + "," + ApplicationRoles.SystemAdmin)]
    public async Task<ActionResult<IReadOnlyList<TeamResponse>>> GetTeams(CancellationToken cancellationToken)
    {
        var teams = await db.Teams
            .AsNoTracking()
            .Where(team => team.IsActive)
            .OrderBy(team => team.Code)
            .Select(team => new TeamResponse(
                team.Id,
                team.Code,
                team.Name,
                team.DisplayName,
                team.IsActive,
                0))
            .ToListAsync(cancellationToken);

        return Ok(teams);
    }

    [HttpGet("teams/manage")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<IReadOnlyList<TeamResponse>>> GetManagedTeams(CancellationToken cancellationToken)
    {
        var teams = await db.Teams
            .AsNoTracking()
            .OrderBy(team => team.Code)
            .Select(team => new TeamResponse(
                team.Id,
                team.Code,
                team.Name,
                team.DisplayName,
                team.IsActive,
                team.Users.Count(user => user.IsActive && !user.IsDeleted)))
            .ToListAsync(cancellationToken);

        return Ok(teams);
    }

    [HttpPost("teams")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<TeamResponse>> CreateTeam(
        [FromBody] TeamSaveRequest request,
        CancellationToken cancellationToken)
    {
        var input = ResolveTeamInput(request, null);
        var duplicateTeam = await FindTeamDuplicateAsync(input, null, cancellationToken);
        if (duplicateTeam is not null && !duplicateTeam.IsActive)
        {
            return InactiveMasterDataExists("Team", duplicateTeam.Id, duplicateTeam.DisplayName);
        }

        var errors = await ValidateTeamAsync(input, null, cancellationToken);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        var team = new Team
        {
            Code = input.Code,
            Name = input.Name,
            DisplayName = input.DisplayName,
            IsActive = true
        };
        db.Teams.Add(team);
        await db.SaveChangesAsync(cancellationToken);

        AddAudit("Team", team.Id.ToString(), "Created", null, Snapshot(team));
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetManagedTeams), new { id = team.Id }, ToResponse(team, 0));
    }

    [HttpPut("teams/{id:int}")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<TeamResponse>> UpdateTeam(
        int id,
        [FromBody] TeamSaveRequest request,
        CancellationToken cancellationToken)
    {
        var team = await db.Teams.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (team is null)
        {
            return NotFound();
        }

        var input = ResolveTeamInput(request, team);
        var errors = await ValidateTeamAsync(input, id, cancellationToken);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        var oldValues = Snapshot(team);
        team.Code = input.Code;
        team.Name = input.Name;
        team.DisplayName = input.DisplayName;
        await db.SaveChangesAsync(cancellationToken);

        AddAudit("Team", team.Id.ToString(), "Updated", oldValues, Snapshot(team));
        await db.SaveChangesAsync(cancellationToken);

        var activeUserCount = await db.Users.CountAsync(
            user => user.TeamId == team.Id && user.IsActive && !user.IsDeleted,
            cancellationToken);
        return Ok(ToResponse(team, activeUserCount));
    }

    [HttpPost("teams/{id:int}/activate")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<TeamResponse>> ActivateTeam(int id, CancellationToken cancellationToken)
    {
        var team = await db.Teams.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (team is null)
        {
            return NotFound();
        }

        if (!team.IsActive)
        {
            var oldValues = Snapshot(team);
            team.IsActive = true;
            await db.SaveChangesAsync(cancellationToken);
            AddAudit("Team", team.Id.ToString(), "Activated", oldValues, Snapshot(team));
            await db.SaveChangesAsync(cancellationToken);
        }

        var activeUserCount = await db.Users.CountAsync(
            user => user.TeamId == team.Id && user.IsActive && !user.IsDeleted,
            cancellationToken);
        return Ok(ToResponse(team, activeUserCount));
    }

    [HttpPost("teams/{id:int}/deactivate")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<TeamResponse>> DeactivateTeam(int id, CancellationToken cancellationToken)
    {
        var team = await db.Teams.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (team is null)
        {
            return NotFound();
        }

        var activeUserCount = await db.Users.CountAsync(
            user => user.TeamId == team.Id && user.IsActive && !user.IsDeleted,
            cancellationToken);
        if (activeUserCount > 0)
        {
            return TeamHasActiveUsers(activeUserCount);
        }

        if (team.IsActive)
        {
            var oldValues = Snapshot(team);
            team.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
            AddAudit("Team", team.Id.ToString(), "Deactivated", oldValues, Snapshot(team));
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(ToResponse(team, activeUserCount));
    }

    [HttpDelete("teams/{id:int}")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<IActionResult> DeleteTeam(int id, CancellationToken cancellationToken)
    {
        var team = await db.Teams.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (team is null)
        {
            return NotFound();
        }

        var activeUserCount = await db.Users.CountAsync(
            user => user.TeamId == team.Id && user.IsActive && !user.IsDeleted,
            cancellationToken);
        if (activeUserCount > 0)
        {
            return TeamHasActiveUsers(activeUserCount, "Team kann nicht gelöscht werden");
        }

        if (team.IsActive)
        {
            var oldValues = Snapshot(team);
            team.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
            AddAudit("Team", team.Id.ToString(), "Deleted", oldValues, Snapshot(team));
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("saving-reasons")]
    [Authorize(Roles = ApplicationRoles.Mitarbeiter + "," + ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<IReadOnlyList<SavingReasonResponse>>> GetSavingReasons(CancellationToken cancellationToken)
    {
        var reasons = await db.SavingReasons
            .AsNoTracking()
            .Where(reason => reason.IsActive)
            .OrderBy(reason => reason.Id)
            .Select(reason => new SavingReasonResponse(reason.Id, reason.Name, reason.IsActive, 0))
            .ToListAsync(cancellationToken);

        return Ok(reasons);
    }

    [HttpGet("saving-reasons/manage")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<IReadOnlyList<SavingReasonResponse>>> GetManagedSavingReasons(CancellationToken cancellationToken)
    {
        var reasons = await db.SavingReasons
            .AsNoTracking()
            .OrderBy(reason => reason.Name)
            .Select(reason => new SavingReasonResponse(
                reason.Id,
                reason.Name,
                reason.IsActive,
                reason.SavingsEntries.Count(entry => !entry.IsDeleted)))
            .ToListAsync(cancellationToken);

        return Ok(reasons);
    }

    [HttpPost("saving-reasons")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<SavingReasonResponse>> CreateSavingReason(
        [FromBody] SavingReasonSaveRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var duplicateReason = await db.SavingReasons
            .FirstOrDefaultAsync(reason => reason.Name.ToLower() == name.ToLower(), cancellationToken);
        if (duplicateReason is not null && !duplicateReason.IsActive)
        {
            return InactiveMasterDataExists("SavingReason", duplicateReason.Id, duplicateReason.Name);
        }

        var errors = await ValidateSavingReasonAsync(name, null, cancellationToken);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        var reason = new SavingReason { Name = name, IsActive = true };
        db.SavingReasons.Add(reason);
        await db.SaveChangesAsync(cancellationToken);
        AddAudit("SavingReason", reason.Id.ToString(), "Created", null, Snapshot(reason));
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetManagedSavingReasons), new { id = reason.Id }, ToResponse(reason, 0));
    }

    [HttpPut("saving-reasons/{id:int}")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<SavingReasonResponse>> UpdateSavingReason(
        int id,
        [FromBody] SavingReasonSaveRequest request,
        CancellationToken cancellationToken)
    {
        var reason = await db.SavingReasons.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (reason is null)
        {
            return NotFound();
        }

        var name = request.Name?.Trim() ?? string.Empty;
        var errors = await ValidateSavingReasonAsync(name, id, cancellationToken);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        var oldValues = Snapshot(reason);
        reason.Name = name;
        await db.SaveChangesAsync(cancellationToken);
        AddAudit("SavingReason", reason.Id.ToString(), "Updated", oldValues, Snapshot(reason));
        await db.SaveChangesAsync(cancellationToken);

        var count = await db.SavingsEntries.CountAsync(
            entry => entry.SavingReasonId == reason.Id && !entry.IsDeleted,
            cancellationToken);
        return Ok(ToResponse(reason, count));
    }

    [HttpPost("saving-reasons/{id:int}/activate")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<SavingReasonResponse>> ActivateSavingReason(int id, CancellationToken cancellationToken)
    {
        var reason = await db.SavingReasons.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (reason is null)
        {
            return NotFound();
        }

        if (!reason.IsActive)
        {
            var oldValues = Snapshot(reason);
            reason.IsActive = true;
            await db.SaveChangesAsync(cancellationToken);
            AddAudit("SavingReason", reason.Id.ToString(), "Activated", oldValues, Snapshot(reason));
            await db.SaveChangesAsync(cancellationToken);
        }

        var count = await db.SavingsEntries.CountAsync(
            entry => entry.SavingReasonId == reason.Id && !entry.IsDeleted,
            cancellationToken);
        return Ok(ToResponse(reason, count));
    }

    [HttpPost("saving-reasons/{id:int}/deactivate")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<SavingReasonResponse>> DeactivateSavingReason(int id, CancellationToken cancellationToken)
    {
        var reason = await db.SavingReasons.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (reason is null)
        {
            return NotFound();
        }

        if (reason.IsActive)
        {
            var oldValues = Snapshot(reason);
            reason.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
            AddAudit("SavingReason", reason.Id.ToString(), "Deactivated", oldValues, Snapshot(reason));
            await db.SaveChangesAsync(cancellationToken);
        }

        var count = await db.SavingsEntries.CountAsync(
            entry => entry.SavingReasonId == reason.Id && !entry.IsDeleted,
            cancellationToken);
        return Ok(ToResponse(reason, count));
    }

    [HttpDelete("saving-reasons/{id:int}")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<IActionResult> DeleteSavingReason(int id, CancellationToken cancellationToken)
    {
        var reason = await db.SavingReasons.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (reason is null)
        {
            return NotFound();
        }

        if (reason.IsActive)
        {
            var oldValues = Snapshot(reason);
            reason.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
            AddAudit("SavingReason", reason.Id.ToString(), "Deleted", oldValues, Snapshot(reason));
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("product-groups")]
    [Authorize(Roles = ApplicationRoles.Mitarbeiter + "," + ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<IReadOnlyList<ProductGroupResponse>>> GetProductGroups(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = db.ProductGroups
            .AsNoTracking()
            .Where(group => group.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(group => group.DisplayValue.Contains(normalizedSearch));
        }

        var groups = await query
            .OrderBy(group => group.DisplayValue)
            .Select(group => new ProductGroupResponse(group.Id, group.DisplayValue, group.IsActive, 0))
            .ToListAsync(cancellationToken);

        return Ok(groups);
    }

    [HttpGet("product-groups/manage")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<IReadOnlyList<ProductGroupResponse>>> GetManagedProductGroups(CancellationToken cancellationToken)
    {
        var groups = await db.ProductGroups
            .AsNoTracking()
            .OrderBy(group => group.DisplayValue)
            .Select(group => new ProductGroupResponse(
                group.Id,
                group.DisplayValue,
                group.IsActive,
                group.SavingsEntries.Count(entry => !entry.IsDeleted)))
            .ToListAsync(cancellationToken);

        return Ok(groups);
    }

    [HttpPost("product-groups")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<ProductGroupResponse>> CreateProductGroup(
        [FromBody] ProductGroupSaveRequest request,
        CancellationToken cancellationToken)
    {
        var displayValue = request.DisplayValue?.Trim() ?? string.Empty;
        var duplicateGroup = await db.ProductGroups
            .FirstOrDefaultAsync(group => group.DisplayValue.ToLower() == displayValue.ToLower(), cancellationToken);
        if (duplicateGroup is not null && !duplicateGroup.IsActive)
        {
            return InactiveMasterDataExists("ProductGroup", duplicateGroup.Id, duplicateGroup.DisplayValue);
        }

        var errors = await ValidateProductGroupAsync(displayValue, null, cancellationToken);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        var group = new ProductGroup
        {
            DisplayValue = displayValue,
            ImportedBy = User.Identity?.Name ?? "manual",
            IsActive = true
        };
        db.ProductGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        AddAudit("ProductGroup", group.Id.ToString(), "Created", null, Snapshot(group));
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetManagedProductGroups), new { id = group.Id }, ToResponse(group, 0));
    }

    [HttpPut("product-groups/{id:int}")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<ProductGroupResponse>> UpdateProductGroup(
        int id,
        [FromBody] ProductGroupSaveRequest request,
        CancellationToken cancellationToken)
    {
        var group = await db.ProductGroups.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        var displayValue = request.DisplayValue?.Trim() ?? string.Empty;
        var errors = await ValidateProductGroupAsync(displayValue, id, cancellationToken);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        var oldValues = Snapshot(group);
        group.DisplayValue = displayValue;
        await db.SaveChangesAsync(cancellationToken);
        AddAudit("ProductGroup", group.Id.ToString(), "Updated", oldValues, Snapshot(group));
        await db.SaveChangesAsync(cancellationToken);

        var count = await db.SavingsEntries.CountAsync(
            entry => entry.ProductGroupId == group.Id && !entry.IsDeleted,
            cancellationToken);
        return Ok(ToResponse(group, count));
    }

    [HttpPost("product-groups/{id:int}/activate")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<ProductGroupResponse>> ActivateProductGroup(int id, CancellationToken cancellationToken)
    {
        var group = await db.ProductGroups.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        if (!group.IsActive)
        {
            var oldValues = Snapshot(group);
            group.IsActive = true;
            await db.SaveChangesAsync(cancellationToken);
            AddAudit("ProductGroup", group.Id.ToString(), "Activated", oldValues, Snapshot(group));
            await db.SaveChangesAsync(cancellationToken);
        }

        var count = await db.SavingsEntries.CountAsync(
            entry => entry.ProductGroupId == group.Id && !entry.IsDeleted,
            cancellationToken);
        return Ok(ToResponse(group, count));
    }

    [HttpPost("product-groups/{id:int}/deactivate")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<ProductGroupResponse>> DeactivateProductGroup(int id, CancellationToken cancellationToken)
    {
        var group = await db.ProductGroups.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        if (group.IsActive)
        {
            var oldValues = Snapshot(group);
            group.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
            AddAudit("ProductGroup", group.Id.ToString(), "Deactivated", oldValues, Snapshot(group));
            await db.SaveChangesAsync(cancellationToken);
        }

        var count = await db.SavingsEntries.CountAsync(
            entry => entry.ProductGroupId == group.Id && !entry.IsDeleted,
            cancellationToken);
        return Ok(ToResponse(group, count));
    }

    [HttpDelete("product-groups/{id:int}")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<IActionResult> DeleteProductGroup(int id, CancellationToken cancellationToken)
    {
        var group = await db.ProductGroups.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        if (group.IsActive)
        {
            var oldValues = Snapshot(group);
            group.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
            AddAudit("ProductGroup", group.Id.ToString(), "Deleted", oldValues, Snapshot(group));
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private Task<Team?> FindTeamDuplicateAsync(
        TeamInput input,
        int? existingId,
        CancellationToken cancellationToken)
    {
        return input.IsOrganizationUnit
            ? db.Teams.FirstOrDefaultAsync(
                team => team.DisplayName.ToLower() == input.DisplayName.ToLower()
                    && (!existingId.HasValue || team.Id != existingId.Value),
                cancellationToken)
            : db.Teams.FirstOrDefaultAsync(
                team => team.Code.ToLower() == input.Code.ToLower()
                    && (!existingId.HasValue || team.Id != existingId.Value),
                cancellationToken);
    }

    private ConflictObjectResult InactiveMasterDataExists(
        string masterDataType,
        int id,
        string displayName)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Stammdatum ist bereits vorhanden, aber deaktiviert",
            Detail = $"{displayName} existiert bereits, ist aber deaktiviert. Sie können den Wert wieder aktivieren.",
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = "MASTER_DATA_INACTIVE_EXISTS";
        problem.Extensions["masterDataType"] = masterDataType;
        problem.Extensions["id"] = id;
        problem.Extensions["displayName"] = displayName;
        problem.Extensions["status"] = "Inaktiv";
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return Conflict(problem);
    }

    private static TeamInput ResolveTeamInput(TeamSaveRequest request, Team? existingTeam)
    {
        var organizationUnit = request.OrganizationUnit?.Trim() ?? string.Empty;
        if (request.OrganizationUnit is not null)
        {
            var code = existingTeam?.Code;
            if (string.IsNullOrWhiteSpace(code) || code.Length > 20)
            {
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(organizationUnit)));
                code = $"OU-{hash[..17]}";
            }

            var name = organizationUnit.Length > 150
                ? organizationUnit[..150]
                : organizationUnit;
            return new TeamInput(code, name, organizationUnit, true);
        }

        var legacyCode = request.Code?.Trim() ?? string.Empty;
        var legacyName = request.Name?.Trim() ?? string.Empty;
        return new TeamInput(legacyCode, legacyName, BuildDisplayName(legacyName, legacyCode), false);
    }

    private async Task<List<string>> ValidateTeamAsync(
        TeamInput input,
        int? existingId,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (input.IsOrganizationUnit)
        {
            if (string.IsNullOrWhiteSpace(input.DisplayName))
            {
                errors.Add("Organisationseinheit ist erforderlich.");
            }
            else if (input.DisplayName.Length > 200)
            {
                errors.Add("Organisationseinheit darf maximal 200 Zeichen lang sein.");
            }

            if (errors.Count == 0 && await db.Teams.AnyAsync(
                    team => team.DisplayName.ToLower() == input.DisplayName.ToLower()
                        && (!existingId.HasValue || team.Id != existingId.Value),
                    cancellationToken))
            {
                errors.Add("Diese Organisationseinheit existiert bereits.");
            }

            return errors;
        }

        var code = input.Code;
        var name = input.Name;
        if (string.IsNullOrWhiteSpace(code))
        {
            errors.Add("Teamcode ist erforderlich.");
        }
        else if (code.Length > 20)
        {
            errors.Add("Teamcode darf maximal 20 Zeichen lang sein.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Teamname ist erforderlich.");
        }
        else if (name.Length > 150)
        {
            errors.Add("Teamname darf maximal 150 Zeichen lang sein.");
        }

        if (errors.Count == 0 && await db.Teams.AnyAsync(
                team => team.Code.ToLower() == code.ToLower() && (!existingId.HasValue || team.Id != existingId.Value),
                cancellationToken))
        {
            errors.Add("Dieser Teamcode ist bereits vergeben.");
        }

        return errors;
    }

    private async Task<List<string>> ValidateSavingReasonAsync(string name, int? existingId, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Einspargrund ist erforderlich.");
        }
        else if (name.Length > 300)
        {
            errors.Add("Einspargrund darf maximal 300 Zeichen lang sein.");
        }

        if (errors.Count == 0 && await db.SavingReasons.AnyAsync(
                reason => reason.Name.ToLower() == name.ToLower() && (!existingId.HasValue || reason.Id != existingId.Value),
                cancellationToken))
        {
            errors.Add("Dieser Einspargrund existiert bereits.");
        }

        return errors;
    }

    private async Task<List<string>> ValidateProductGroupAsync(string displayValue, int? existingId, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(displayValue))
        {
            errors.Add("Produktgruppe ist erforderlich.");
        }
        else if (displayValue.Length > 500)
        {
            errors.Add("Produktgruppe darf maximal 500 Zeichen lang sein.");
        }

        if (errors.Count == 0 && await db.ProductGroups.AnyAsync(
                group => group.DisplayValue.ToLower() == displayValue.ToLower() && (!existingId.HasValue || group.Id != existingId.Value),
                cancellationToken))
        {
            errors.Add("Diese Produktgruppe existiert bereits.");
        }

        return errors;
    }

    private ConflictObjectResult TeamHasActiveUsers(int count, string title = "Team kann nicht deaktiviert werden")
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = title,
            Detail = $"Dem Team sind noch {count} aktive Benutzer zugeordnet. Benutzer müssen zunächst umgezogen oder deaktiviert werden.",
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = "TEAM_HAS_ACTIVE_USERS";
        problem.Extensions["activeUserCount"] = count;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return Conflict(problem);
    }

    private sealed record TeamInput(string Code, string Name, string DisplayName, bool IsOrganizationUnit);

    private static string BuildDisplayName(string name, string code) => $"{name} ({code})";

    private static TeamResponse ToResponse(Team team, int activeUserCount) =>
        new(team.Id, team.Code, team.Name, team.DisplayName, team.IsActive, activeUserCount);

    private static SavingReasonResponse ToResponse(SavingReason reason, int referencedSavingsCount) =>
        new(reason.Id, reason.Name, reason.IsActive, referencedSavingsCount);

    private static ProductGroupResponse ToResponse(ProductGroup group, int referencedSavingsCount) =>
        new(group.Id, group.DisplayValue, group.IsActive, referencedSavingsCount);

    private static object Snapshot(Team team) => new
    {
        team.Id,
        team.Code,
        team.Name,
        team.DisplayName,
        team.IsActive
    };

    private static object Snapshot(SavingReason reason) => new
    {
        reason.Id,
        reason.Name,
        reason.IsActive
    };

    private static object Snapshot(ProductGroup group) => new
    {
        group.Id,
        group.DisplayValue,
        group.IsActive
    };

    private void AddAudit(string entityName, string entityId, string action, object? oldValues, object? newValues)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(SecurityClaims.UserId);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new InvalidOperationException("Der aktuelle Benutzer konnte nicht aus der Sitzung ermittelt werden.");
        }

        db.AuditLogs.Add(new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow,
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues, JsonOptions),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues, JsonOptions),
            ChangedFieldsJson = DetermineChangedFieldsJson(oldValues, newValues),
            ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        });
    }

    private static string DetermineChangedFieldsJson(object? oldValues, object? newValues)
    {
        if (oldValues is null && newValues is null)
        {
            return "[]";
        }

        using var oldDocument = oldValues is null
            ? null
            : JsonDocument.Parse(JsonSerializer.Serialize(oldValues, JsonOptions));
        using var newDocument = newValues is null
            ? null
            : JsonDocument.Parse(JsonSerializer.Serialize(newValues, JsonOptions));

        var names = new HashSet<string>(StringComparer.Ordinal);
        if (oldDocument is not null)
        {
            foreach (var property in oldDocument.RootElement.EnumerateObject())
            {
                names.Add(property.Name);
            }
        }
        if (newDocument is not null)
        {
            foreach (var property in newDocument.RootElement.EnumerateObject())
            {
                names.Add(property.Name);
            }
        }

        var changed = names
            .Where(name => !JsonValuesEqual(
                oldDocument?.RootElement.TryGetProperty(name, out var oldValue) == true ? oldValue : null,
                newDocument?.RootElement.TryGetProperty(name, out var newValue) == true ? newValue : null))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        return JsonSerializer.Serialize(changed, JsonOptions);
    }

    private static bool JsonValuesEqual(JsonElement? oldValue, JsonElement? newValue)
    {
        if (!oldValue.HasValue || !newValue.HasValue)
        {
            return !oldValue.HasValue && !newValue.HasValue;
        }

        return oldValue.Value.GetRawText() == newValue.Value.GetRawText();
    }
}
