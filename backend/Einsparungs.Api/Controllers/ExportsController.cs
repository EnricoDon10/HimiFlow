using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/exports")]
[Authorize(Roles = ApplicationRoles.FachAdmin)]
public sealed class ExportsController : ControllerBase
{
    private readonly AppDbContext db;
    private readonly bool maskKvnrInExports;
    private readonly int maximumRows;

    public ExportsController(AppDbContext db, IConfiguration configuration)
    {
        this.db = db;
        maskKvnrInExports = configuration.GetValue("Privacy:MaskKvnrInExports", true);
        maximumRows = Math.Clamp(configuration.GetValue("Exports:MaximumRows", 10_000), 1, 1_000_000);
    }

    [HttpGet("savings.csv")]
    public async Task<IActionResult> ExportSavingsCsv(
        [FromQuery] ExportSavingsQuery request,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(SavingsQuery(), request);
        var count = await query.CountAsync(cancellationToken);
        if (count > maximumRows)
        {
            return ExportLimitExceeded(count);
        }

        var fileName = $"einsparungen_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        await AddExportAuditAsync("Csv", fileName, count, request, cancellationToken);
        AddNoStoreHeaders(fileName, "text/csv; charset=utf-8");

        await using var writer = new StreamWriter(
            Response.Body,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            bufferSize: 16 * 1024,
            leaveOpen: true);
        await writer.WriteLineAsync(
            $"Id;Monat;{(maskKvnrInExports ? "KVNR (maskiert)" : "KVNR")};Alter KV;Neuer KV;Ersparnis;Team;Einspargrund;Produktgruppe;Uebermittlungsdatum;Erstellt von;Erstellt am;Version");

        await foreach (var row in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            var line = string.Join(";", new[]
            {
                Csv(row.Id.ToString()),
                Csv(row.Month.ToString("MM.yyyy", GermanCulture)),
                Csv(ExportKvnr(row.Kvnr)),
                Csv(row.OldKvAmount.ToString("N2", GermanCulture)),
                Csv(row.NewKvAmount.ToString("N2", GermanCulture)),
                Csv(row.SavingAmount.ToString("N2", GermanCulture)),
                Csv(row.TeamName),
                Csv(row.SavingReasonName),
                Csv(row.ProductGroupDisplayValue),
                Csv(row.TransmissionDate.ToString("dd.MM.yyyy HH:mm:ss", GermanCulture)),
                Csv(row.CreatedByDisplayName),
                Csv(row.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss", GermanCulture)),
                Csv(row.Version.ToString(CultureInfo.InvariantCulture))
            });
            await writer.WriteLineAsync(line);
        }

        await writer.FlushAsync(cancellationToken);
        return new EmptyResult();
    }

    [HttpGet("savings.xlsx")]
    public async Task<IActionResult> ExportSavingsExcel(
        [FromQuery] ExportSavingsQuery request,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(SavingsQuery(), request);
        var count = await query.CountAsync(cancellationToken);
        if (count > maximumRows)
        {
            return ExportLimitExceeded(count);
        }

        var rows = await query.ToListAsync(cancellationToken);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Einsparungen");
        var headers = new[]
        {
            "Id", "Monat", maskKvnrInExports ? "KVNR (maskiert)" : "KVNR", "Alter KV", "Neuer KV",
            "Ersparnis", "Team", "Einspargrund", "Produktgruppe", "Uebermittlungsdatum", "Erstellt von",
            "Erstellt am", "Version"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            SetTextCell(worksheet.Cell(1, i + 1), headers[i]);
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;
            SetTextCell(worksheet.Cell(excelRow, 1), row.Id.ToString());
            worksheet.Cell(excelRow, 2).Value = row.Month;
            SetTextCell(worksheet.Cell(excelRow, 3), ExportKvnr(row.Kvnr));
            worksheet.Cell(excelRow, 4).Value = row.OldKvAmount;
            worksheet.Cell(excelRow, 5).Value = row.NewKvAmount;
            worksheet.Cell(excelRow, 6).Value = row.SavingAmount;
            SetTextCell(worksheet.Cell(excelRow, 7), row.TeamName);
            SetTextCell(worksheet.Cell(excelRow, 8), row.SavingReasonName);
            SetTextCell(worksheet.Cell(excelRow, 9), row.ProductGroupDisplayValue);
            worksheet.Cell(excelRow, 10).Value = row.TransmissionDate;
            SetTextCell(worksheet.Cell(excelRow, 11), row.CreatedByDisplayName);
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
        await AddExportAuditAsync("Excel", fileName, count, request, cancellationToken);
        AddNoStoreHeaders(fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private IQueryable<ExportSavingsRow> SavingsQuery()
    {
        return db.SavingsEntries
            .AsNoTracking()
            .Where(entry => !entry.IsDeleted)
            .OrderByDescending(entry => entry.Month)
            .ThenByDescending(entry => entry.CreatedAt)
            .Select(entry => new ExportSavingsRow
            {
                Id = entry.Id,
                Month = entry.Month,
                Kvnr = entry.Kvnr,
                OldKvAmount = entry.OldKvAmount,
                NewKvAmount = entry.NewKvAmount,
                SavingAmount = entry.SavingAmount,
                TeamId = entry.TeamId,
                SavingReasonId = entry.SavingReasonId,
                ProductGroupId = entry.ProductGroupId,
                TeamName = entry.Team.DisplayName,
                SavingReasonName = entry.SavingReason.Name,
                ProductGroupDisplayValue = entry.ProductGroup.DisplayValue,
                TransmissionDate = entry.TransmissionDate,
                CreatedByDisplayName = entry.CreatedByUser.DisplayName,
                CreatedAt = entry.CreatedAt,
                Version = entry.Version
            });
    }

    private static IQueryable<ExportSavingsRow> ApplyFilters(IQueryable<ExportSavingsRow> query, ExportSavingsQuery request)
    {
        if (request.Month.HasValue)
        {
            var month = new DateTime(request.Month.Value.Year, request.Month.Value.Month, 1);
            query = query.Where(row => row.Month >= month && row.Month < month.AddMonths(1));
        }
        if (request.TeamId.HasValue) query = query.Where(row => row.TeamId == request.TeamId.Value);
        if (request.SavingReasonId.HasValue) query = query.Where(row => row.SavingReasonId == request.SavingReasonId.Value);
        if (request.ProductGroupId.HasValue) query = query.Where(row => row.ProductGroupId == request.ProductGroupId.Value);
        return query;
    }

    private ObjectResult ExportLimitExceeded(int count)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status413PayloadTooLarge,
            Title = "Exportumfang zu groß",
            Detail = $"Der gefilterte Export umfasst {count} Datensätze und überschreitet das konfigurierte Limit von {maximumRows}.",
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = "EXPORT_LIMIT_EXCEEDED";
        problem.Extensions["maximumRows"] = maximumRows;
        problem.Extensions["actualRows"] = count;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return StatusCode(StatusCodes.Status413PayloadTooLarge, problem);
    }

    private static string Csv(string? value)
    {
        var safeValue = SpreadsheetInjectionProtection.NeutralizeText(value);
        return $"\"{safeValue.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static void SetTextCell(IXLCell cell, string? value)
    {
        // ClosedXML infers a string assignment as text. Dangerous prefixes are
        // additionally apostrophe-prefixed by NeutralizeText so Excel cannot evaluate them.
        cell.Value = SpreadsheetInjectionProtection.NeutralizeText(value);
    }

    private string ExportKvnr(string kvnr) => maskKvnrInExports ? PrivacyMasking.MaskKvnr(kvnr) : kvnr;

    private void AddNoStoreHeaders(string fileName, string contentType)
    {
        Response.ContentType = contentType;
        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }

    private async Task AddExportAuditAsync(string format, string fileName, int count, ExportSavingsQuery filters, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return;
        db.AuditLogs.Add(new AuditLog
        {
            EntityName = "SavingsExport",
            EntityId = format,
            Action = "Exported",
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow,
            NewValuesJson = JsonSerializer.Serialize(new { format, fileName, count, filters, exportedAtUtc = DateTime.UtcNow }),
            ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

    private sealed class ExportSavingsRow
    {
        public Guid Id { get; init; }
        public DateTime Month { get; init; }
        public string Kvnr { get; init; } = string.Empty;
        public decimal OldKvAmount { get; init; }
        public decimal NewKvAmount { get; init; }
        public decimal SavingAmount { get; init; }
        public int TeamId { get; init; }
        public int SavingReasonId { get; init; }
        public int ProductGroupId { get; init; }
        public string TeamName { get; init; } = string.Empty;
        public string SavingReasonName { get; init; } = string.Empty;
        public string ProductGroupDisplayValue { get; init; } = string.Empty;
        public DateTime TransmissionDate { get; init; }
        public string CreatedByDisplayName { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public int Version { get; init; }
    }
}
