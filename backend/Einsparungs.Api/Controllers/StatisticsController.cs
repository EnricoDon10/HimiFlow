using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/statistics")]
[Authorize(Roles = ApplicationRoles.Mitarbeiter + "," + ApplicationRoles.FachAdmin)]
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
        var aggregate = await BaseStatisticsQuery()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                EntryCount = group.Count(),
                TotalSavingAmount = group.Sum(entry => entry.SavingAmount),
                AverageSavingAmount = group.Average(entry => entry.SavingAmount),
                HighestSavingAmount = group.Max(entry => entry.SavingAmount),
                LowestSavingAmount = group.Min(entry => entry.SavingAmount)
            })
            .SingleOrDefaultAsync();

        if (aggregate is null)
        {
            return Ok(new StatisticsOverviewResponse());
        }

        return Ok(new StatisticsOverviewResponse
        {
            EntryCount = aggregate.EntryCount,
            TotalSavingAmount = RoundMoney(aggregate.TotalSavingAmount),
            AverageSavingAmount = RoundMoney(aggregate.AverageSavingAmount),
            HighestSavingAmount = RoundMoney(aggregate.HighestSavingAmount),
            LowestSavingAmount = RoundMoney(aggregate.LowestSavingAmount)
        });
    }

    [HttpGet("monthly")]
    public async Task<ActionResult<List<MonthlySavingsStatisticResponse>>> GetMonthly()
    {
        var aggregates = await BaseStatisticsQuery()
            .GroupBy(x => new
            {
                x.Month.Year,
                x.Month.Month
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                EntryCount = group.Count(),
                TotalSavingAmount = group.Sum(entry => entry.SavingAmount),
                AverageSavingAmount = group.Average(entry => entry.SavingAmount)
            })
            .OrderBy(item => item.Year)
            .ThenBy(item => item.Month)
            .ToListAsync();

        var result = aggregates.Select(item => new MonthlySavingsStatisticResponse
        {
            Year = item.Year,
            Month = item.Month,
            MonthLabel = $"{item.Month:00}.{item.Year}",
            EntryCount = item.EntryCount,
            TotalSavingAmount = RoundMoney(item.TotalSavingAmount),
            AverageSavingAmount = RoundMoney(item.AverageSavingAmount)
        }).ToList();

        return Ok(result);
    }

    [HttpGet("by-team")]
    public async Task<ActionResult<List<GroupedSavingsStatisticResponse>>> GetByTeam()
    {
        var aggregates = await BaseStatisticsQuery()
            .GroupBy(x => new
            {
                Key = x.TeamId.ToString(),
                Name = x.Team.DisplayName
            })
            .Select(group => new
            {
                GroupKey = group.Key.Key,
                GroupName = group.Key.Name,
                EntryCount = group.Count(),
                TotalSavingAmount = group.Sum(entry => entry.SavingAmount),
                AverageSavingAmount = group.Average(entry => entry.SavingAmount)
            })
            .OrderByDescending(x => x.TotalSavingAmount)
            .ThenBy(x => x.GroupName)
            .ToListAsync();

        var result = aggregates.Select(item => new GroupedSavingsStatisticResponse
        {
            GroupKey = item.GroupKey,
            GroupName = item.GroupName,
            EntryCount = item.EntryCount,
            TotalSavingAmount = RoundMoney(item.TotalSavingAmount),
            AverageSavingAmount = RoundMoney(item.AverageSavingAmount)
        }).ToList();

        return Ok(result);
    }

    [HttpGet("by-saving-reason")]
    public async Task<ActionResult<List<GroupedSavingsStatisticResponse>>> GetBySavingReason()
    {
        var aggregates = await BaseStatisticsQuery()
            .GroupBy(x => new
            {
                Key = x.SavingReasonId.ToString(),
                Name = x.SavingReason.Name
            })
            .Select(group => new
            {
                GroupKey = group.Key.Key,
                GroupName = group.Key.Name,
                EntryCount = group.Count(),
                TotalSavingAmount = group.Sum(entry => entry.SavingAmount),
                AverageSavingAmount = group.Average(entry => entry.SavingAmount)
            })
            .OrderByDescending(x => x.TotalSavingAmount)
            .ThenBy(x => x.GroupName)
            .ToListAsync();

        var result = aggregates.Select(item => new GroupedSavingsStatisticResponse
        {
            GroupKey = item.GroupKey,
            GroupName = item.GroupName,
            EntryCount = item.EntryCount,
            TotalSavingAmount = RoundMoney(item.TotalSavingAmount),
            AverageSavingAmount = RoundMoney(item.AverageSavingAmount)
        }).ToList();

        return Ok(result);
    }

    [HttpGet("by-product-group")]
    public async Task<ActionResult<List<GroupedSavingsStatisticResponse>>> GetByProductGroup()
    {
        var aggregates = await BaseStatisticsQuery()
            .GroupBy(x => new
            {
                Key = x.ProductGroupId.ToString(),
                Name = x.ProductGroup.DisplayValue
            })
            .Select(group => new
            {
                GroupKey = group.Key.Key,
                GroupName = group.Key.Name,
                EntryCount = group.Count(),
                TotalSavingAmount = group.Sum(entry => entry.SavingAmount),
                AverageSavingAmount = group.Average(entry => entry.SavingAmount)
            })
            .OrderByDescending(x => x.TotalSavingAmount)
            .ThenBy(x => x.GroupName)
            .ToListAsync();

        var result = aggregates.Select(item => new GroupedSavingsStatisticResponse
        {
            GroupKey = item.GroupKey,
            GroupName = item.GroupName,
            EntryCount = item.EntryCount,
            TotalSavingAmount = RoundMoney(item.TotalSavingAmount),
            AverageSavingAmount = RoundMoney(item.AverageSavingAmount)
        }).ToList();

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
