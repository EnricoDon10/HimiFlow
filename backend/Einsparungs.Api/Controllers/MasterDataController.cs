using Einsparungs.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/master-data")]
public class MasterDataController : ControllerBase
{
    private readonly AppDbContext _db;

    public MasterDataController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("teams")]
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
            .Take(50)
            .ToListAsync();

        return Ok(productGroups);
    }
}