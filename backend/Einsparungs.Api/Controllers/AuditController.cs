using System.Text.Json;
using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/admin/audit")]
[Authorize(Roles = ApplicationRoles.SystemAdmin)]
public sealed class AuditController : ControllerBase
{
    private readonly AppDbContext db;

    public AuditController(AppDbContext db)
    {
        this.db = db;
    }

    [HttpGet]
    public async Task<ActionResult<AuditLogPageResponse>> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? entityName = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 100_000 || pageSize is < 1 or > 100)
        {
            return BadRequest(new { errors = new[] { "page muss zwischen 1 und 100000 und pageSize zwischen 1 und 100 liegen." } });
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return BadRequest(new { errors = new[] { "from darf nicht nach to liegen." } });
        }

        var query = db.AuditLogs
            .AsNoTracking()
            .Include(log => log.ChangedByUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(log => log.EntityName == entityName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(log => log.Action == action.Trim());
        }

        if (from.HasValue)
        {
            query = query.Where(log => log.ChangedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(log => log.ChangedAt <= to.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rawItems = await query
            .OrderByDescending(log => log.ChangedAt)
            .ThenByDescending(log => log.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new
            {
                log.Id,
                log.EntityName,
                log.EntityId,
                log.Action,
                log.ChangedByUserId,
                ChangedByUserName = log.ChangedByUser.UserName,
                log.ChangedAt,
                log.ClientIp,
                log.UserAgent,
                log.ChangedFieldsJson,
                HasOldValues = log.OldValuesJson != null,
                HasNewValues = log.NewValuesJson != null
            })
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(log => new AuditLogResponse(
                log.Id,
                log.EntityName,
                log.EntityId,
                log.Action,
                log.ChangedByUserId,
                log.ChangedByUserName,
                log.ChangedAt,
                log.ClientIp,
                log.UserAgent,
                ParseChangedFields(log.ChangedFieldsJson),
                log.HasOldValues,
                log.HasNewValues))
            .ToArray();

        return Ok(new AuditLogPageResponse(items, page, pageSize, totalCount));
    }

    private static IReadOnlyList<string> ParseChangedFields(string? changedFieldsJson)
    {
        if (string.IsNullOrWhiteSpace(changedFieldsJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(changedFieldsJson) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
