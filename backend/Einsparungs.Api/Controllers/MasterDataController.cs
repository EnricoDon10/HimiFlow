using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/master-data")]
[Authorize]
public class MasterDataController : ControllerBase
{
    private readonly AppDbContext _db;

    public MasterDataController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("teams")]
    [Authorize(Roles = ApplicationRoles.Mitarbeiter + "," + ApplicationRoles.FachAdmin + "," + ApplicationRoles.SystemAdmin)]
    public async Task<IActionResult> GetTeams()
    {
        var teams = await _db.Teams
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.DisplayName
            })
            .ToListAsync();

        return Ok(teams);
    }

    [HttpGet("saving-reasons")]
    [Authorize(Roles = ApplicationRoles.Mitarbeiter + "," + ApplicationRoles.FachAdmin)]
    public async Task<IActionResult> GetSavingReasons()
    {
        var reasons = await _db.SavingReasons
            .Where(x => x.IsActive)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Name
            })
            .ToListAsync();

        return Ok(reasons);
    }

    [HttpGet("product-groups")]
    [Authorize(Roles = ApplicationRoles.Mitarbeiter + "," + ApplicationRoles.FachAdmin)]
    public async Task<IActionResult> GetProductGroups([FromQuery] string? search)
    {
        var query = _db.ProductGroups
            .Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.DisplayValue.Contains(search));
        }

        var productGroups = await query
            .OrderBy(x => x.DisplayValue)
            .Select(x => new
            {
                x.Id,
                x.DisplayValue
            })
            .ToListAsync();

        return Ok(productGroups);
    }

    [HttpGet("product-groups/manage")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<IActionResult> GetManagedProductGroups()
    {
        var productGroups = await _db.ProductGroups
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayValue)
            .Select(x => new ProductGroupResponse(x.Id, x.DisplayValue))
            .ToListAsync();

        return Ok(productGroups);
    }

    [HttpPost("product-groups")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<ProductGroupResponse>> CreateProductGroup(ProductGroupSaveRequest request)
    {
        var displayValue = request.DisplayValue?.Trim() ?? string.Empty;
        var errors = await ValidateProductGroupAsync(displayValue);

        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        var productGroup = new ProductGroup
        {
            DisplayValue = displayValue,
            ImportedBy = User.Identity?.Name ?? "manual"
        };

        _db.ProductGroups.Add(productGroup);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetManagedProductGroups),
            new { id = productGroup.Id },
            new ProductGroupResponse(productGroup.Id, productGroup.DisplayValue)
        );
    }

    [HttpPut("product-groups/{id:int}")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<ActionResult<ProductGroupResponse>> UpdateProductGroup(
        int id,
        ProductGroupSaveRequest request)
    {
        var productGroup = await _db.ProductGroups
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive);

        if (productGroup is null)
        {
            return NotFound();
        }

        var displayValue = request.DisplayValue?.Trim() ?? string.Empty;
        var errors = await ValidateProductGroupAsync(displayValue, id);

        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        productGroup.DisplayValue = displayValue;
        await _db.SaveChangesAsync();

        return Ok(new ProductGroupResponse(productGroup.Id, productGroup.DisplayValue));
    }

    [HttpDelete("product-groups/{id:int}")]
    [Authorize(Roles = ApplicationRoles.FachAdmin)]
    public async Task<IActionResult> DeleteProductGroup(int id)
    {
        var productGroup = await _db.ProductGroups
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive);

        if (productGroup is null)
        {
            return NotFound();
        }

        productGroup.IsActive = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<List<string>> ValidateProductGroupAsync(string displayValue, int? existingId = null)
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

        if (!string.IsNullOrWhiteSpace(displayValue))
        {
            var normalizedValue = displayValue.ToLower();

            var alreadyExists = await _db.ProductGroups.AnyAsync(x =>
                x.IsActive &&
                x.DisplayValue.ToLower() == normalizedValue &&
                (!existingId.HasValue || x.Id != existingId.Value));

            if (alreadyExists)
            {
                errors.Add("Diese Produktgruppe existiert bereits.");
            }
        }

        return errors;
    }
}

public sealed record ProductGroupSaveRequest(string DisplayValue);

public sealed record ProductGroupResponse(int Id, string DisplayValue);
