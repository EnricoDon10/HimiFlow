using System.Text.Json;
using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/savings")]
[Authorize]
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
    public async Task<ActionResult<List<SavingsEntryResponse>>> GetMySavings()
    {
        var currentUserId = GetCurrentUserId();

        var entries = await SavingsResponseQuery()
            .Where(x => x.CreatedByUserId == currentUserId)
            .OrderByDescending(x => x.Month)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(entries);
    }

    [HttpGet("all")]
    [Authorize(Roles = "Fuehrungskraft,Admin")]
    public async Task<ActionResult<List<SavingsEntryResponse>>> GetAllSavings()
    {
        var entries = await SavingsResponseQuery()
            .OrderByDescending(x => x.Month)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(entries);
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

    [HttpPost]
    public async Task<ActionResult<SavingsEntryResponse>> Create([FromBody] SavingsEntryCreateRequest request)
    {
        var currentUserId = GetCurrentUserId();

        var validationErrors = await ValidateSavingsRequestAsync(
            request.Month,
            request.Kvnr,
            request.OldKvAmount,
            request.NewKvAmount,
            request.TeamId,
            request.SavingReasonId,
            request.ProductGroupId
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
            TeamId = request.TeamId,
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
            newValues: CreateAuditSnapshot(entry)
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

        var validationErrors = await ValidateSavingsRequestAsync(
            request.Month,
            request.Kvnr,
            request.OldKvAmount,
            request.NewKvAmount,
            request.TeamId,
            request.SavingReasonId,
            request.ProductGroupId
        );

        if (validationErrors.Count > 0)
        {
            return BadRequest(new { errors = validationErrors });
        }

        var oldSnapshot = CreateAuditSnapshot(entry);

        var oldKvAmount = RoundMoney(request.OldKvAmount);
        var newKvAmount = RoundMoney(request.NewKvAmount);

        entry.Month = NormalizeMonth(request.Month);
        entry.Kvnr = request.Kvnr.Trim();
        entry.OldKvAmount = oldKvAmount;
        entry.NewKvAmount = newKvAmount;
        entry.SavingAmount = RoundMoney(oldKvAmount - newKvAmount);
        entry.TeamId = request.TeamId;
        entry.SavingReasonId = request.SavingReasonId;
        entry.ProductGroupId = request.ProductGroupId;
        entry.UpdatedByUserId = currentUserId;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.Version += 1;

        AddAuditLog(
            entry.Id,
            "Updated",
            currentUserId,
            oldValues: oldSnapshot,
            newValues: CreateAuditSnapshot(entry)
        );

        await _db.SaveChangesAsync();

        var response = await SavingsResponseQuery()
            .SingleAsync(x => x.Id == entry.Id);

        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
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

        var oldSnapshot = CreateAuditSnapshot(entry);

        entry.IsDeleted = true;
        entry.DeletedByUserId = currentUserId;
        entry.DeletedAt = DateTime.UtcNow;
        entry.Version += 1;

        AddAuditLog(
            entry.Id,
            "Deleted",
            currentUserId,
            oldValues: oldSnapshot,
            newValues: CreateAuditSnapshot(entry)
        );

        await _db.SaveChangesAsync();

        return NoContent();
    }

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
                CreatedByUserName = x.CreatedByUser.UserName,
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
        int productGroupId)
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

        var teamExists = await _db.Teams.AnyAsync(x => x.Id == teamId && x.IsActive);
        if (!teamExists)
        {
            errors.Add("Das ausgewählte Team ist ungültig.");
        }

        var reasonExists = await _db.SavingReasons.AnyAsync(x => x.Id == savingReasonId && x.IsActive);
        if (!reasonExists)
        {
            errors.Add("Der ausgewählte Einspargrund ist ungültig.");
        }

        var productGroupExists = await _db.ProductGroups.AnyAsync(x => x.Id == productGroupId && x.IsActive);
        if (!productGroupExists)
        {
            errors.Add("Die ausgewählte PG Nummer ist ungültig.");
        }

        return errors;
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirst("userId")?.Value;

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new InvalidOperationException("Der aktuelle Benutzer konnte nicht aus dem Token ermittelt werden.");
        }

        return userId;
    }

    private bool CanManageAllRecords()
    {
        return User.IsInRole("Fuehrungskraft") || User.IsInRole("Admin");
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

    private static object CreateAuditSnapshot(SavingsEntry entry)
    {
        return new
        {
            entry.Id,
            entry.Month,
            entry.Kvnr,
            entry.OldKvAmount,
            entry.NewKvAmount,
            entry.SavingAmount,
            entry.TeamId,
            entry.SavingReasonId,
            entry.ProductGroupId,
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
}

