namespace Einsparungs.Api.DTOs;

public class StatisticsOverviewResponse
{
    public int EntryCount { get; set; }

    public decimal TotalSavingAmount { get; set; }

    public decimal AverageSavingAmount { get; set; }

    public decimal HighestSavingAmount { get; set; }

    public decimal LowestSavingAmount { get; set; }
}
