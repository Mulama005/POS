using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Roles = "Manager,Admin")]
public class AuditController : ControllerBase
{
    private readonly PosDbContext _context;

    public AuditController(PosDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] string? userName = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? actionType = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(userName))
            query = query.Where(a => EF.Functions.ILike(a.User.FullName, $"%{userName.Trim()}%"));

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(a => a.UserId.ToString() == userId.Trim());

        if (!string.IsNullOrEmpty(actionType))
            query = query.Where(a => EF.Functions.ILike(a.ActionType, $"%{actionType}%"));

        if (fromDate.HasValue)
            query = query.Where(a => a.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(a => a.Timestamp <= toDate.Value);
        
        var orderedQuery = query.OrderByDescending(a => a.Timestamp);

        var total = await orderedQuery.CountAsync();

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.Timestamp,
                UserName = a.User.FullName,
                a.ActionType,
                a.EntityName,
                a.EntityId,
                a.Details,
                a.IpAddress
            })
            .ToListAsync();

        return Ok(new
        {
            items,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportAuditLogPdf(
        [FromQuery] string? userName = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? actionType = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var query = _context.AuditLogs.Include(a => a.User).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(userName))
            query = query.Where(a => EF.Functions.ILike(a.User.FullName, $"%{userName.Trim()}%"));
        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(a => a.UserId.ToString() == userId.Trim());
        if (!string.IsNullOrWhiteSpace(actionType))
            query = query.Where(a => EF.Functions.ILike(a.ActionType, $"%{actionType.Trim()}%"));
        if (fromDate.HasValue) query = query.Where(a => a.Timestamp >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(a => a.Timestamp <= toDate.Value);

        var entries = await query.OrderByDescending(a => a.Timestamp)
            .Select(a => new { a.Timestamp, User = a.User.FullName, a.ActionType, a.EntityName, a.EntityId, a.Details, a.IpAddress })
            .ToListAsync();
        var document = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1.25f, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontFamily("Helvetica").FontSize(8).FontColor(Colors.Grey.Darken4));
            page.Header().Column(header =>
            {
                header.Item().Text("AYIYAPOS · SYSTEM MONITORING").FontSize(9).SemiBold().FontColor(Colors.BlueGrey.Darken3).LetterSpacing(1.2f);
                header.Item().PaddingTop(4).Text("Audit log").FontSize(22).Bold().FontColor(Colors.Grey.Darken4);
                header.Item().PaddingTop(3).Text($"{entries.Count:N0} matching records · Generated {DateTime.UtcNow.AddHours(3):dd MMM yyyy, HH:mm} EAT").FontColor(Colors.Grey.Darken1);
                header.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });
            page.Content().PaddingTop(12).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(100); columns.ConstantColumn(110); columns.ConstantColumn(92);
                    columns.ConstantColumn(85); columns.ConstantColumn(88); columns.RelativeColumn(3); columns.RelativeColumn(1);
                });
                table.Header(header =>
                {
                    foreach (var label in new[] { "TIMESTAMP (EAT)", "STAFF MEMBER", "ACTION", "ENTITY", "ENTITY ID", "DETAILS", "IP ADDRESS" })
                        header.Cell().Background(Colors.Grey.Lighten3).PaddingVertical(7).PaddingHorizontal(5).Text(label).FontSize(7).SemiBold().FontColor(Colors.Grey.Darken2);
                });
                foreach (var entry in entries)
                {
                    var nairobiTimestamp = DateTime.SpecifyKind(entry.Timestamp, DateTimeKind.Utc).AddHours(3);
                    var values = new[] { nairobiTimestamp.ToString("yyyy-MM-dd HH:mm:ss"), entry.User, entry.ActionType, entry.EntityName, entry.EntityId.ToString(), entry.Details ?? "", entry.IpAddress ?? "" };
                    foreach (var value in values)
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(6).PaddingHorizontal(5).Text(value).FontSize(7);
                }
            });
            page.Footer().AlignCenter().Text(text => { text.Span("AyiyaPOS | Audit log | Page "); text.CurrentPageNumber(); text.Span(" of "); text.TotalPages(); });
        }));
        return File(document.GeneratePdf(), "application/pdf", $"AyiyaPOS_Audit_Log_{DateTime.UtcNow:yyyyMMdd_HHmm}.pdf");
    }
}
