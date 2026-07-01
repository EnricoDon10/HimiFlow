using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/statistics")]
[Authorize]
public class StatisticsController : ControllerBase
{
    private readonly AppDbContext _db;

    public StatisticsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<StatisticsOverviewResponse>> GetOverview()
    {
        var entries = await BaseStatisticsQuery().ToListAsync();

        if (entries.Count == 0)
        {
            return Ok(new StatisticsOverviewResponse());
        }

        return Ok(new StatisticsOverviewResponse
        {
            EntryCount = entries.Count,
            TotalSavingAmount = RoundMoney(entries.Sum(x => x.SavingAmount)),
            AverageSavingAmount = RoundMoney(entries.Average(x => x.SavingAmount)),
            HighestSavingAmount = RoundMoney(entries.Max(x => x.SavingAmount)),
            LowestSavingAmount = RoundMoney(entries.Min(x => x.SavingAmount))
        });
    }

    [HttpGet("monthly")]
    public async Task<ActionResult<List<MonthlySavingsStatisticResponse>>> GetMonthly()
    {
        var entries = await BaseStatisticsQuery().ToListAsync();

        var result = entries
            .GroupBy(x => new
            {
                x.Month.Year,
                x.Month.Month
            })
            .Select(x => new MonthlySavingsStatisticResponse
            {
                Year = x.Key.Year,
                Month = x.Key.Month,
                MonthLabel = $"{x.Key.Month:00}.{x.Key.Year}",
                EntryCount = x.Count(),
                TotalSavingAmount = RoundMoney(x.Sum(y => y.SavingAmount)),
                AverageSavingAmount = RoundMoney(x.Average(y => y.SavingAmount))
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToList();

        return Ok(result);
    }

    [HttpGet("by-team")]
    public async Task<ActionResult<List<GroupedSavingsStatisticResponse>>> GetByTeam()
    {
        var entries = await BaseStatisticsQuery()
            .Include(x => x.Team)
            .ToListAsync();

        var result = entries
            .GroupBy(x => new
            {
                Key = x.TeamId.ToString(),
                Name = x.Team.DisplayName
            })
            .Select(x => new GroupedSavingsStatisticResponse
            {
                GroupKey = x.Key.Key,
                GroupName = x.Key.Name,
                EntryCount = x.Count(),
                TotalSavingAmount = RoundMoney(x.Sum(y => y.SavingAmount)),
                AverageSavingAmount = RoundMoney(x.Average(y => y.SavingAmount))
            })
            .OrderByDescending(x => x.TotalSavingAmount)
            .ThenBy(x => x.GroupName)
            .ToList();

        return Ok(result);
    }

    [HttpGet("by-saving-reason")]
    public async Task<ActionResult<List<GroupedSavingsStatisticResponse>>> GetBySavingReason()
    {
        var entries = await BaseStatisticsQuery()
            .Include(x => x.SavingReason)
            .ToListAsync();

        var result = entries
            .GroupBy(x => new
            {
                Key = x.SavingReasonId.ToString(),
                Name = x.SavingReason.Name
            })
            .Select(x => new GroupedSavingsStatisticResponse
            {
                GroupKey = x.Key.Key,
                GroupName = x.Key.Name,
                EntryCount = x.Count(),
                TotalSavingAmount = RoundMoney(x.Sum(y => y.SavingAmount)),
                AverageSavingAmount = RoundMoney(x.Average(y => y.SavingAmount))
            })
            .OrderByDescending(x => x.TotalSavingAmount)
            .ThenBy(x => x.GroupName)
            .ToList();

        return Ok(result);
    }

    [HttpGet("by-product-group")]
    public async Task<ActionResult<List<GroupedSavingsStatisticResponse>>> GetByProductGroup()
    {
        var entries = await BaseStatisticsQuery()
            .Include(x => x.ProductGroup)
            .ToListAsync();

        var result = entries
            .GroupBy(x => new
            {
                Key = x.ProductGroupId.ToString(),
                Name = x.ProductGroup.DisplayValue
            })
            .Select(x => new GroupedSavingsStatisticResponse
            {
                GroupKey = x.Key.Key,
                GroupName = x.Key.Name,
                EntryCount = x.Count(),
                TotalSavingAmount = RoundMoney(x.Sum(y => y.SavingAmount)),
                AverageSavingAmount = RoundMoney(x.Average(y => y.SavingAmount))
            })
            .OrderByDescending(x => x.TotalSavingAmount)
            .ThenBy(x => x.GroupName)
            .ToList();

        return Ok(result);
    }

    private IQueryable<SavingsEntry> BaseStatisticsQuery()
    {
        return _db.SavingsEntries
            .AsNoTracking()
            .Where(x => !x.IsDeleted);
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
