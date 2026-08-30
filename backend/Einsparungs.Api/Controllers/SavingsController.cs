using System.Text.Json;
using Einsparungs.Api.Data;
using System.Globalization;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/savings")]
[Authorize(Roles = ApplicationRoles.Mitarbeiter + "," + ApplicationRoles.FachAdmin)]
public class SavingsController : ControllerBase
{
    private readonly AppDbContext _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public SavingsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("my")]
    public async Task<ActionResult<PagedResponse<SavingsEntryResponse>>> GetMySavings(
        [FromQuery] SavingsListQuery request)
    {
        if (!IsValidPagination(request))
        {
            return InvalidPagination();
        }

        var currentUserId = GetCurrentUserId();

        var query = SavingsResponseQuery()
            .Where(x => x.CreatedByUserId == currentUserId);
        query = ApplyFilters(query, request);

        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);
        var entries = await query
            .OrderByDescending(x => x.Month)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return Ok(new PagedResponse<SavingsEntryResponse>(
            entries,
            request.Page,
            request.PageSize,
            totalCount,
            totalPages));
    }

    [HttpGet("all")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<PagedResponse<SavingsEntryResponse>>> GetAllSavings(
        [FromQuery] SavingsListQuery request)
    {
        if (!IsValidPagination(request))
        {
            return InvalidPagination();
        }

        var query = SavingsResponseQuery();

        query = ApplyFilters(query, request);

        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);
        var entries = await query
            .OrderByDescending(x => x.Month)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return Ok(new PagedResponse<SavingsEntryResponse>(
            entries,
            request.Page,
            request.PageSize,
            totalCount,
            totalPages));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SavingsEntryResponse>> GetById(Guid id)
    {
        var currentUserId = GetCurrentUserId();

        var rawEntry = await _db.SavingsEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (rawEntry is null)
        {
            return NotFound();
        }

        if (!CanManageAllRecords() && rawEntry.CreatedByUserId != currentUserId)
        {
            return Forbid();
        }

        var response = await SavingsResponseQuery()
            .SingleAsync(x => x.Id == id);

        return Ok(response);
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<IReadOnlyList<SavingsHistoryEntryResponse>>> GetHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var logs = await _db.AuditLogs
            .AsNoTracking()
            .Where(log => log.EntityName == "SavingsEntry" && log.EntityId == id.ToString())
            .OrderByDescending(log => log.ChangedAt)
            .ThenByDescending(log => log.Id)
            .Select(log => new
            {
                log.Id,
                log.Action,
                log.ChangedAt,
                log.ChangedByUserId,
                ChangedByDisplayName = log.ChangedByUser.DisplayName,
                log.OldValuesJson,
                log.NewValuesJson
            })
            .ToListAsync(cancellationToken);

        if (logs.Count == 0)
        {
            return NotFound();
        }

        return Ok(logs.Select(log => new SavingsHistoryEntryResponse(
            log.Id,
            log.Action,
            log.ChangedAt,
            log.ChangedByUserId,
            log.ChangedByDisplayName,
            CreateHistoryChanges(log.OldValuesJson, log.NewValuesJson))).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<SavingsEntryResponse>> Create([FromBody] SavingsEntryCreateRequest request)
    {
        var currentUserId = GetCurrentUserId();
        var teamResolution = await ResolveTeamForWriteAsync(currentUserId, request.TeamId);
        if (!teamResolution.IsAllowed)
        {
            return TeamScopeProblem(teamResolution);
        }

        var validationErrors = await ValidateSavingsRequestAsync(
            request.Month,
            request.Kvnr,
            request.OldKvAmount,
            request.NewKvAmount,
            teamResolution.TeamId,
            request.SavingReasonId,
            request.ProductGroupId,
            allowInactiveTeamId: null,
            allowInactiveSavingReasonId: null,
            allowInactiveProductGroupId: null
        );

        if (validationErrors.Count > 0)
        {
            return BadRequest(new { errors = validationErrors });
        }

        var oldKvAmount = RoundMoney(request.OldKvAmount);
        var newKvAmount = RoundMoney(request.NewKvAmount);

        var entry = new SavingsEntry
        {
            Id = Guid.NewGuid(),
            Month = NormalizeMonth(request.Month),
            Kvnr = request.Kvnr.Trim(),
            OldKvAmount = oldKvAmount,
            NewKvAmount = newKvAmount,
            SavingAmount = RoundMoney(oldKvAmount - newKvAmount),
            TeamId = teamResolution.TeamId,
            SavingReasonId = request.SavingReasonId,
            ProductGroupId = request.ProductGroupId,
            TransmissionDate = DateTime.UtcNow,
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        _db.SavingsEntries.Add(entry);

        AddAuditLog(
            entry.Id,
            "Created",
            currentUserId,
            oldValues: null,
            newValues: await CreateAuditSnapshotAsync(entry)
        );

        await _db.SaveChangesAsync();

        var response = await SavingsResponseQuery()
            .SingleAsync(x => x.Id == entry.Id);

        return CreatedAtAction(nameof(GetById), new { id = entry.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SavingsEntryResponse>> Update(Guid id, [FromBody] SavingsEntryUpdateRequest request)
    {
        var currentUserId = GetCurrentUserId();

        var entry = await _db.SavingsEntries
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (entry is null)
        {
            return NotFound();
        }

        if (!CanManageAllRecords() && entry.CreatedByUserId != currentUserId)
        {
            return Forbid();
        }

        var teamResolution = await ResolveTeamForWriteAsync(currentUserId, request.TeamId);
        if (!teamResolution.IsAllowed)
        {
            return TeamScopeProblem(teamResolution);
        }

        if (entry.Version != request.ExpectedVersion)
        {
            return ConcurrencyConflict();
        }

        var validationErrors = await ValidateSavingsRequestAsync(
            request.Month,
            request.Kvnr,
            request.OldKvAmount,
            request.NewKvAmount,
            teamResolution.TeamId,
            request.SavingReasonId,
            request.ProductGroupId,
            allowInactiveTeamId: entry.TeamId,
            allowInactiveSavingReasonId: entry.SavingReasonId,
            allowInactiveProductGroupId: entry.ProductGroupId
        );

        if (validationErrors.Count > 0)
        {
            return BadRequest(new { errors = validationErrors });
        }

        var oldSnapshot = await CreateAuditSnapshotAsync(entry);

        var oldKvAmount = RoundMoney(request.OldKvAmount);
        var newKvAmount = RoundMoney(request.NewKvAmount);

        entry.Month = NormalizeMonth(request.Month);
        entry.Kvnr = request.Kvnr.Trim();
        entry.OldKvAmount = oldKvAmount;
        entry.NewKvAmount = newKvAmount;
        entry.SavingAmount = RoundMoney(oldKvAmount - newKvAmount);
        entry.TeamId = teamResolution.TeamId;
        entry.SavingReasonId = request.SavingReasonId;
        entry.ProductGroupId = request.ProductGroupId;
        entry.UpdatedByUserId = currentUserId;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.Version = request.ExpectedVersion + 1;

        AddAuditLog(
            entry.Id,
            "Updated",
            currentUserId,
            oldValues: oldSnapshot,
            newValues: await CreateAuditSnapshotAsync(entry)
        );

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }

        var response = await SavingsResponseQuery()
            .SingleAsync(x => x.Id == entry.Id);

        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] int? expectedVersion)
    {
        var currentUserId = GetCurrentUserId();

        var entry = await _db.SavingsEntries
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (entry is null)
        {
            return NotFound();
        }

        if (!CanManageAllRecords() && entry.CreatedByUserId != currentUserId)
        {
            return Forbid();
        }

        if (!expectedVersion.HasValue)
        {
            return ExpectedVersionRequired();
        }

        if (entry.Version != expectedVersion.Value)
        {
            return ConcurrencyConflict();
        }

        var oldSnapshot = await CreateAuditSnapshotAsync(entry);

        entry.IsDeleted = true;
        entry.DeletedByUserId = currentUserId;
        entry.DeletedAt = DateTime.UtcNow;
        entry.Version += 1;

        AddAuditLog(
            entry.Id,
            "Deleted",
            currentUserId,
            oldValues: oldSnapshot,
            newValues: await CreateAuditSnapshotAsync(entry)
        );

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }

        return NoContent();
    }

    private static IQueryable<SavingsEntryResponse> ApplyFilters(
        IQueryable<SavingsEntryResponse> query,
        SavingsListQuery request)
    {
        if (request.Month.HasValue)
        {
            var month = NormalizeMonth(request.Month.Value);
            var nextMonth = month.AddMonths(1);
            query = query.Where(entry => entry.Month >= month && entry.Month < nextMonth);
        }

        if (request.TeamId.HasValue)
        {
            query = query.Where(entry => entry.TeamId == request.TeamId.Value);
        }

        if (request.SavingReasonId.HasValue)
        {
            query = query.Where(entry => entry.SavingReasonId == request.SavingReasonId.Value);
        }

        if (request.ProductGroupId.HasValue)
        {
            query = query.Where(entry => entry.ProductGroupId == request.ProductGroupId.Value);
        }

        if (request.CreatedByUserId.HasValue)
        {
            query = query.Where(entry => entry.CreatedByUserId == request.CreatedByUserId.Value);
        }

        return query;
    }

    private static bool IsValidPagination(SavingsListQuery request) =>
        request.Page is >= 1 and <= 1_000_000 && request.PageSize is >= 1 and <= 100;

    private BadRequestObjectResult InvalidPagination() => BadRequest(new
    {
        code = "INVALID_PAGINATION",
        errors = new[] { "Page muss mindestens 1 und PageSize muss zwischen 1 und 100 liegen." }
    });

    private IQueryable<SavingsEntryResponse> SavingsResponseQuery()
    {
        return _db.SavingsEntries
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new SavingsEntryResponse
            {
                Id = x.Id,
                Month = x.Month,
                Kvnr = x.Kvnr,
                OldKvAmount = x.OldKvAmount,
                NewKvAmount = x.NewKvAmount,
                SavingAmount = x.SavingAmount,
                TeamId = x.TeamId,
                TeamName = x.Team.DisplayName,
                SavingReasonId = x.SavingReasonId,
                SavingReasonName = x.SavingReason.Name,
                ProductGroupId = x.ProductGroupId,
                ProductGroupDisplayValue = x.ProductGroup.DisplayValue,
                TransmissionDate = x.TransmissionDate,
                CreatedByUserId = x.CreatedByUserId,
                CreatedByUserName = x.CreatedByUser.UserName ?? string.Empty,
                CreatedByDisplayName = x.CreatedByUser.DisplayName,
                CreatedAt = x.CreatedAt,
                UpdatedByUserId = x.UpdatedByUserId,
                UpdatedAt = x.UpdatedAt,
                Version = x.Version
            });
    }

    private async Task<List<string>> ValidateSavingsRequestAsync(
        DateTime month,
        string kvnr,
        decimal oldKvAmount,
        decimal newKvAmount,
        int teamId,
        int savingReasonId,
        int productGroupId,
        int? allowInactiveTeamId,
        int? allowInactiveSavingReasonId,
        int? allowInactiveProductGroupId)
    {
        var errors = new List<string>();

        if (month == default)
        {
            errors.Add("Monat ist ein Pflichtfeld.");
        }

        if (string.IsNullOrWhiteSpace(kvnr))
        {
            errors.Add("KVNR ist ein Pflichtfeld.");
        }
        else if (!IsValidKvnr(kvnr.Trim()))
        {
            errors.Add("KVNR muss aus einem Großbuchstaben und genau 9 Ziffern bestehen.");
        }

        if (oldKvAmount < 0)
        {
            errors.Add("Alter KV darf nicht kleiner als 0 sein.");
        }

        if (newKvAmount < 0)
        {
            errors.Add("Neuer KV darf nicht kleiner als 0 sein.");
        }

        if (newKvAmount > oldKvAmount)
        {
            errors.Add("Neuer KV muss kleiner oder gleich alter KV sein.");
        }

        var teamExists = await _db.Teams.AnyAsync(x =>
            x.Id == teamId && (x.IsActive || x.Id == allowInactiveTeamId));
        if (!teamExists)
        {
            errors.Add("Das ausgewählte Team ist ungültig.");
        }

        var reasonExists = await _db.SavingReasons.AnyAsync(x =>
            x.Id == savingReasonId && (x.IsActive || x.Id == allowInactiveSavingReasonId));
        if (!reasonExists)
        {
            errors.Add("Der ausgewählte Einspargrund ist ungültig.");
        }

        var productGroupExists = await _db.ProductGroups.AnyAsync(x =>
            x.Id == productGroupId && (x.IsActive || x.Id == allowInactiveProductGroupId));
        if (!productGroupExists)
        {
            errors.Add("Die ausgewählte PG Nummer ist ungültig.");
        }

        return errors;
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                          User.FindFirstValue(SecurityClaims.UserId);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new InvalidOperationException("Der aktuelle Benutzer konnte nicht aus der Sitzung ermittelt werden.");
        }

        return userId;
    }

    private bool CanManageAllRecords()
    {
        return User.IsInRole(ApplicationRoles.FachAdmin);
    }

    private static bool IsValidKvnr(string kvnr)
    {
        if (kvnr.Length != 10)
        {
            return false;
        }

        if (kvnr[0] < 'A' || kvnr[0] > 'Z')
        {
            return false;
        }

        for (var i = 1; i < kvnr.Length; i++)
        {
            if (!char.IsDigit(kvnr[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static DateTime NormalizeMonth(DateTime month)
    {
        return new DateTime(month.Year, month.Month, 1);
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<object> CreateAuditSnapshotAsync(SavingsEntry entry)
    {
        var teamName = await _db.Teams
            .Where(item => item.Id == entry.TeamId)
            .Select(item => item.DisplayName)
            .SingleOrDefaultAsync();
        var savingReasonName = await _db.SavingReasons
            .Where(item => item.Id == entry.SavingReasonId)
            .Select(item => item.Name)
            .SingleOrDefaultAsync();
        var productGroupDisplayValue = await _db.ProductGroups
            .Where(item => item.Id == entry.ProductGroupId)
            .Select(item => item.DisplayValue)
            .SingleOrDefaultAsync();

        return new
        {
            entry.Id,
            entry.Month,
            Kvnr = PrivacyMasking.MaskKvnr(entry.Kvnr),
            entry.OldKvAmount,
            entry.NewKvAmount,
            entry.SavingAmount,
            entry.TeamId,
            TeamName = teamName,
            entry.SavingReasonId,
            SavingReasonName = savingReasonName,
            entry.ProductGroupId,
            ProductGroupDisplayValue = productGroupDisplayValue,
            entry.TransmissionDate,
            entry.CreatedByUserId,
            entry.CreatedAt,
            entry.UpdatedByUserId,
            entry.UpdatedAt,
            entry.IsDeleted,
            entry.DeletedByUserId,
            entry.DeletedAt,
            entry.Version
        };
    }

    private static IReadOnlyList<SavingsFieldChangeResponse> CreateHistoryChanges(
        string? oldValuesJson,
        string? newValuesJson)
    {
        using var oldDocument = ParseAuditDocument(oldValuesJson);
        using var newDocument = ParseAuditDocument(newValuesJson);
        var fields = new[]
        {
            new HistoryField("month", "Monat", HistoryValueKind.Month),
            new HistoryField("kvnr", "KVNR (maskiert)", HistoryValueKind.Kvnr),
            new HistoryField("oldKvAmount", "Alter KV", HistoryValueKind.Money),
            new HistoryField("newKvAmount", "Neuer KV", HistoryValueKind.Money),
            new HistoryField("savingAmount", "Ersparnis", HistoryValueKind.Money),
            new HistoryField("teamId", "Team", HistoryValueKind.Team),
            new HistoryField("savingReasonId", "Einspargrund", HistoryValueKind.SavingReason),
            new HistoryField("productGroupId", "Produktgruppe", HistoryValueKind.ProductGroup),
            new HistoryField("isDeleted", "Gelöscht", HistoryValueKind.Boolean)
        };

        var changes = new List<SavingsFieldChangeResponse>();
        foreach (var field in fields)
        {
            var oldValue = ReadHistoryValue(oldDocument?.RootElement, field);
            var newValue = ReadHistoryValue(newDocument?.RootElement, field);

            if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                continue;
            }

            changes.Add(new SavingsFieldChangeResponse(
                field.Name,
                field.Label,
                oldValue,
                newValue));
        }

        return changes;
    }

    private static JsonDocument? ParseAuditDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadHistoryValue(JsonElement? root, HistoryField field)
    {
        if (!root.HasValue ||
            root.Value.ValueKind != JsonValueKind.Object ||
            !root.Value.TryGetProperty(field.Name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return field.Kind switch
        {
            HistoryValueKind.Kvnr => PrivacyMasking.MaskKvnr(value.GetString()),
            HistoryValueKind.Month when value.TryGetDateTime(out var month) =>
                month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            HistoryValueKind.Money when value.TryGetDecimal(out var money) =>
                money.ToString("F2", CultureInfo.InvariantCulture),
            HistoryValueKind.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False =>
                value.GetBoolean() ? "Ja" : "Nein",
            HistoryValueKind.Team => ReadDisplayNameOrId(root.Value, value, "teamName", "Team-ID"),
            HistoryValueKind.SavingReason => ReadDisplayNameOrId(root.Value, value, "savingReasonName", "Einspargrund-ID"),
            HistoryValueKind.ProductGroup => ReadDisplayNameOrId(root.Value, value, "productGroupDisplayValue", "Produktgruppen-ID"),
            _ => value.ToString()
        };
    }

    private static string ReadDisplayNameOrId(JsonElement root, JsonElement idValue, string displayProperty, string idLabel)
    {
        if (root.TryGetProperty(displayProperty, out var displayValue) &&
            displayValue.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(displayValue.GetString()))
        {
            return displayValue.GetString()!;
        }

        return $"{idLabel} {idValue}";
    }

    private BadRequestObjectResult ExpectedVersionRequired()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Versionsstand erforderlich",
            Detail = "Zum Löschen muss der aktuell angezeigte Versionsstand mitgesendet werden.",
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = "EXPECTED_VERSION_REQUIRED";
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return BadRequest(problem);
    }

    private void AddAuditLog(Guid entityId, string action, Guid changedByUserId, object? oldValues, object? newValues)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = "SavingsEntry",
            EntityId = entityId.ToString(),
            Action = action,
            ChangedByUserId = changedByUserId,
            ChangedAt = DateTime.UtcNow,
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues, JsonOptions),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues, JsonOptions),
            ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        });
    }

    private async Task<TeamResolution> ResolveTeamForWriteAsync(Guid currentUserId, int requestedTeamId)
    {
        if (CanManageAllRecords())
        {
            return TeamResolution.Allowed(requestedTeamId);
        }

        var assignedTeamId = await _db.Users
            .AsNoTracking()
            .Where(user => user.Id == currentUserId && user.IsActive && !user.IsDeleted)
            .Select(user => user.TeamId)
            .SingleOrDefaultAsync();

        if (!assignedTeamId.HasValue)
        {
            return TeamResolution.Denied(
                "TEAM_ASSIGNMENT_REQUIRED",
                "Dem angemeldeten Mitarbeiter ist kein Team zugeordnet. Bitte wenden Sie sich an den System-Admin.");
        }

        if (assignedTeamId.Value != requestedTeamId)
        {
            return TeamResolution.Denied(
                "TEAM_SCOPE_VIOLATION",
                "Mitarbeiter dürfen Einsparungen ausschließlich für ihr eigenes Team erfassen oder bearbeiten.");
        }

        return TeamResolution.Allowed(assignedTeamId.Value);
    }

    private ObjectResult TeamScopeProblem(TeamResolution resolution)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Teamzuordnung nicht zulässig",
            Detail = resolution.Error,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = resolution.Code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return StatusCode(StatusCodes.Status403Forbidden, problem);
    }

    private ConflictObjectResult ConcurrencyConflict()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Änderungskonflikt",
            Detail = "Der Datensatz wurde zwischenzeitlich von einem anderen Benutzer geändert. Bitte laden Sie die aktuellen Daten neu.",
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = "CONCURRENCY_CONFLICT";
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return Conflict(problem);
    }

    private sealed record TeamResolution(bool IsAllowed, int TeamId, string? Code, string? Error)
    {
        public static TeamResolution Allowed(int teamId) => new(true, teamId, null, null);

        public static TeamResolution Denied(string code, string error) => new(false, 0, code, error);
    }

    private sealed record HistoryField(string Name, string Label, HistoryValueKind Kind);

    private enum HistoryValueKind
    {
        Default,
        Month,
        Kvnr,
        Money,
        Boolean,
        Team,
        SavingReason,
        ProductGroup
    }

}

