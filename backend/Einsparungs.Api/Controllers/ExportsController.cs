using System.Globalization;
using System.Security.Claims;
using System.Text;
using ClosedXML.Excel;
using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/exports")]
[Authorize(Roles = ApplicationRoles.FachAdmin)]
public class ExportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly bool _maskKvnrInExports;

    public ExportsController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _maskKvnrInExports = configuration.GetValue("Privacy:MaskKvnrInExports", true);
    }

    [HttpGet("savings.csv")]
    public async Task<IActionResult> ExportSavingsCsv()
    {
        var rows = await GetExportRowsAsync();

        var csv = new StringBuilder();

        csv.AppendLine(
            $"Id;Monat;{(_maskKvnrInExports ? "KVNR (maskiert)" : "KVNR")};Alter KV;Neuer KV;Ersparnis;Team;Einspargrund;Produktgruppe;Uebermittlungsdatum;Erstellt von;Erstellt am;Version"
        );

        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(";", new[]
            {
                Csv(row.Id.ToString()),
                Csv(row.Month.ToString("MM.yyyy", CultureInfo.GetCultureInfo("de-DE"))),
                Csv(ExportKvnr(row.Kvnr)),
                Csv(row.OldKvAmount.ToString("N2", CultureInfo.GetCultureInfo("de-DE"))),
                Csv(row.NewKvAmount.ToString("N2", CultureInfo.GetCultureInfo("de-DE"))),
                Csv(row.SavingAmount.ToString("N2", CultureInfo.GetCultureInfo("de-DE"))),
                Csv(row.TeamName),
                Csv(row.SavingReasonName),
                Csv(row.ProductGroupDisplayValue),
                Csv(row.TransmissionDate.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.GetCultureInfo("de-DE"))),
                Csv(row.CreatedByDisplayName),
                Csv(row.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.GetCultureInfo("de-DE"))),
                Csv(row.Version.ToString(CultureInfo.InvariantCulture))
            }));
        }

        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
            .ToArray();

        var fileName = $"einsparungen_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

        await AddExportAuditAsync("Csv", fileName);
        AddNoStoreHeaders();

        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    [HttpGet("savings.xlsx")]
    public async Task<IActionResult> ExportSavingsExcel()
    {
        var rows = await GetExportRowsAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Einsparungen");

        var headers = new[]
        {
            "Id",
            "Monat",
            _maskKvnrInExports ? "KVNR (maskiert)" : "KVNR",
            "Alter KV",
            "Neuer KV",
            "Ersparnis",
            "Team",
            "Einspargrund",
            "Produktgruppe",
            "Uebermittlungsdatum",
            "Erstellt von",
            "Erstellt am",
            "Version"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;

            worksheet.Cell(excelRow, 1).Value = row.Id.ToString();
            worksheet.Cell(excelRow, 2).Value = row.Month;
            worksheet.Cell(excelRow, 3).Value = ExportKvnr(row.Kvnr);
            worksheet.Cell(excelRow, 4).Value = row.OldKvAmount;
            worksheet.Cell(excelRow, 5).Value = row.NewKvAmount;
            worksheet.Cell(excelRow, 6).Value = row.SavingAmount;
            worksheet.Cell(excelRow, 7).Value = row.TeamName;
            worksheet.Cell(excelRow, 8).Value = row.SavingReasonName;
            worksheet.Cell(excelRow, 9).Value = row.ProductGroupDisplayValue;
            worksheet.Cell(excelRow, 10).Value = row.TransmissionDate;
            worksheet.Cell(excelRow, 11).Value = row.CreatedByDisplayName;
            worksheet.Cell(excelRow, 12).Value = row.CreatedAt;
            worksheet.Cell(excelRow, 13).Value = row.Version;
        }

        worksheet.Column(2).Style.DateFormat.Format = "MM.yyyy";
        worksheet.Column(4).Style.NumberFormat.Format = "#,##0.00";
        worksheet.Column(5).Style.NumberFormat.Format = "#,##0.00";
        worksheet.Column(6).Style.NumberFormat.Format = "#,##0.00";
        worksheet.Column(10).Style.DateFormat.Format = "dd.MM.yyyy HH:mm:ss";
        worksheet.Column(12).Style.DateFormat.Format = "dd.MM.yyyy HH:mm:ss";

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var fileName = $"einsparungen_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        await AddExportAuditAsync("Excel", fileName);
        AddNoStoreHeaders();

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName
        );
    }

    private async Task<List<ExportSavingsRow>> GetExportRowsAsync()
    {
        return await _db.SavingsEntries
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.Month)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new ExportSavingsRow
            {
                Id = x.Id,
                Month = x.Month,
                Kvnr = x.Kvnr,
                OldKvAmount = x.OldKvAmount,
                NewKvAmount = x.NewKvAmount,
                SavingAmount = x.SavingAmount,
                TeamName = x.Team.DisplayName,
                SavingReasonName = x.SavingReason.Name,
                ProductGroupDisplayValue = x.ProductGroup.DisplayValue,
                TransmissionDate = x.TransmissionDate,
                CreatedByDisplayName = x.CreatedByUser.DisplayName,
                CreatedAt = x.CreatedAt,
                Version = x.Version
            })
            .ToListAsync();
    }

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private string ExportKvnr(string kvnr)
    {
        return _maskKvnrInExports
            ? PrivacyMasking.MaskKvnr(kvnr)
            : kvnr;
    }

    private void AddNoStoreHeaders()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }

    private async Task AddExportAuditAsync(string format, string fileName)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = "SavingsExport",
            EntityId = format,
            Action = "Exported",
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow,
            NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new { format, fileName }),
            ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        });
        await _db.SaveChangesAsync();
    }

    private class ExportSavingsRow
    {
        public Guid Id { get; set; }

        public DateTime Month { get; set; }

        public string Kvnr { get; set; } = string.Empty;

        public decimal OldKvAmount { get; set; }

        public decimal NewKvAmount { get; set; }

        public decimal SavingAmount { get; set; }

        public string TeamName { get; set; } = string.Empty;

        public string SavingReasonName { get; set; } = string.Empty;

        public string ProductGroupDisplayValue { get; set; } = string.Empty;

        public DateTime TransmissionDate { get; set; }

        public string CreatedByDisplayName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public int Version { get; set; }
    }
}
