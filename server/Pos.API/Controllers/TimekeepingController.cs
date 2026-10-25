using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/timekeeping")]
[Authorize(Roles = "Manager,Admin")]
public sealed class TimekeepingController : ControllerBase
{
    private static readonly TimeSpan NairobiOffset = TimeSpan.FromHours(3);
    private readonly PosDbContext _db;
    private readonly IAuditService _audit;

    public TimekeepingController(PosDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken ct)
    {
        var (from, toExclusive, fromDay, toDay) = GetRange(fromDate, toDate);
        if (fromDay > toDay) return BadRequest("The start date must be on or before the end date.");

        var entries = await _db.TechnicianTimeEntries.AsNoTracking()
            .Where(x => x.ClockInAt < toExclusive && (x.ClockOutAt == null || x.ClockOutAt > from))
            .OrderByDescending(x => x.ClockInAt)
            .Select(x => new
            {
                x.Id,
                x.TechnicianId,
                TechnicianName = _db.DomainUsers.Where(u => u.Id == x.TechnicianId).Select(u => u.FullName).FirstOrDefault() ?? "Unknown technician",
                x.ClockInAt,
                x.ClockInRecordedByUserId,
                ClockInRecordedBy = _db.DomainUsers.Where(u => u.Id == x.ClockInRecordedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "Unknown user",
                x.ClockOutAt,
                x.ClockOutRecordedByUserId,
                ClockOutRecordedBy = x.ClockOutRecordedByUserId == null ? null : _db.DomainUsers.Where(u => u.Id == x.ClockOutRecordedByUserId).Select(u => u.FullName).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var rows = entries.Select(x => new
        {
            x.Id,
            x.TechnicianId,
            x.TechnicianName,
            x.ClockInAt,
            x.ClockInRecordedByUserId,
            x.ClockInRecordedBy,
            x.ClockOutAt,
            x.ClockOutRecordedByUserId,
            x.ClockOutRecordedBy,
            IsOpen = x.ClockOutAt is null,
            HoursWorked = Math.Round(Math.Max(0, (((x.ClockOutAt ?? now) > toExclusive ? toExclusive : (x.ClockOutAt ?? now)) - (x.ClockInAt < from ? from : x.ClockInAt)).TotalHours), 2),
        }).ToList();
        var technicians = await _db.DomainUsers.AsNoTracking()
            .Where(u => u.IsActive && u.Role == RegisterUserRole.Technician)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync(ct);

        return Ok(new
        {
            fromDate = fromDay.ToString("yyyy-MM-dd"),
            toDate = toDay.ToString("yyyy-MM-dd"),
            technicians,
            entries = rows,
            totalHours = Math.Round(rows.Sum(x => x.HoursWorked), 2),
            openShifts = rows.Count(x => x.IsOpen),
        });
    }

    [HttpPost("clock-in")]
    public async Task<IActionResult> ClockIn([FromBody] ClockInRequest request, CancellationToken ct)
    {
        var technician = await _db.DomainUsers.FirstOrDefaultAsync(
            u => u.Id == request.TechnicianId && u.IsActive && u.Role == RegisterUserRole.Technician, ct);
        if (technician is null) return BadRequest("Choose an active technician.");

        var entry = new TechnicianTimeEntry
        {
            TechnicianId = technician.Id,
            ClockInAt = DateTimeOffset.UtcNow,
            ClockInRecordedByUserId = CurrentUserId,
        };
        _db.TechnicianTimeEntries.Add(entry);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return Conflict("This technician already has an open shift. Clock them out before starting another.");
        }

        await _audit.LogAsync(CurrentUserId, "TECHNICIAN_CLOCKED_IN", "TechnicianTimeEntry", entry.Id,
            $"Clocked in {technician.FullName} at {entry.ClockInAt.ToOffset(NairobiOffset):yyyy-MM-dd HH:mm} EAT.", HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(new { entry.Id, technicianId = technician.Id, technicianName = technician.FullName, entry.ClockInAt, isOpen = true });
    }

    [HttpPost("{id:guid}/clock-out")]
    public async Task<IActionResult> ClockOut(Guid id, CancellationToken ct)
    {
        var clockOutAt = DateTimeOffset.UtcNow;
        var updated = await _db.TechnicianTimeEntries
            .Where(x => x.Id == id && x.ClockOutAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ClockOutAt, clockOutAt)
                .SetProperty(x => x.ClockOutRecordedByUserId, CurrentUserId), ct);
        if (updated == 0)
        {
            var exists = await _db.TechnicianTimeEntries.AnyAsync(x => x.Id == id, ct);
            return exists ? Conflict("This shift has already been closed.") : NotFound();
        }

        var entry = await _db.TechnicianTimeEntries.AsNoTracking().FirstAsync(x => x.Id == id, ct);
        var technicianName = await _db.DomainUsers.Where(u => u.Id == entry.TechnicianId).Select(u => u.FullName).FirstAsync(ct);
        await _audit.LogAsync(CurrentUserId, "TECHNICIAN_CLOCKED_OUT", "TechnicianTimeEntry", entry.Id,
            $"Clocked out {technicianName} at {clockOutAt.ToOffset(NairobiOffset):yyyy-MM-dd HH:mm} EAT.", HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(new { entry.Id, technicianId = entry.TechnicianId, technicianName, entry.ClockInAt, entry.ClockOutAt, isOpen = false });
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf([FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken ct)
    {
        var (from, toExclusive, fromDay, toDay) = GetRange(fromDate, toDate);
        if (fromDay > toDay) return BadRequest("The start date must be on or before the end date.");

        var entries = await _db.TechnicianTimeEntries.AsNoTracking()
            .Where(x => x.ClockInAt < toExclusive && (x.ClockOutAt == null || x.ClockOutAt > from))
            .OrderBy(x => x.ClockInAt)
            .Select(x => new
            {
                x.ClockInAt,
                x.ClockOutAt,
                Technician = _db.DomainUsers.Where(u => u.Id == x.TechnicianId).Select(u => u.FullName).FirstOrDefault() ?? "Unknown technician",
                RecordedBy = _db.DomainUsers.Where(u => u.Id == x.ClockInRecordedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "Unknown user",
                ClockOutRecordedBy = x.ClockOutRecordedByUserId == null ? null : _db.DomainUsers.Where(u => u.Id == x.ClockOutRecordedByUserId).Select(u => u.FullName).FirstOrDefault(),
            }).ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var totalHours = entries.Sum(x =>
        {
            var start = x.ClockInAt < from ? from : x.ClockInAt;
            var clockOut = x.ClockOutAt ?? now;
            var end = clockOut > toExclusive ? toExclusive : clockOut;
            return Math.Max(0, (end - start).TotalHours);
        });
        var document = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontFamily("Helvetica").FontSize(9));
            page.Header().Column(column =>
            {
                column.Item().Text("TECHNICIAN TIMEKEEPING SUMMARY").FontSize(18).Bold();
                column.Item().PaddingTop(4).Text($"{fromDay:dd MMM yyyy} — {toDay:dd MMM yyyy} · EAT (UTC+3)").FontColor(Colors.Grey.Darken1);
                column.Item().PaddingTop(4).Text($"Generated {DateTimeOffset.UtcNow.ToOffset(NairobiOffset):dd MMM yyyy HH:mm} EAT · {entries.Count} shift(s) · {totalHours:0.00} total hours").FontColor(Colors.Grey.Darken1);
            });
            page.Content().PaddingTop(18).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.5f); columns.RelativeColumn(1.3f); columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(1.5f); columns.RelativeColumn(1.5f); columns.RelativeColumn(1.1f);
                });
                table.Header(header =>
                {
                    foreach (var title in new[] { "Technician", "Clock in (EAT)", "Clock out (EAT)", "Clock-in recorded by", "Clock-out recorded by", "Hours" })
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text(title).SemiBold();
                });
                foreach (var row in entries)
                {
                    var rangeStart = row.ClockInAt < from ? from : row.ClockInAt;
                    var clockOut = row.ClockOutAt ?? now;
                    var rangeEnd = clockOut > toExclusive ? toExclusive : clockOut;
                    var hours = Math.Max(0, (rangeEnd - rangeStart).TotalHours);
                    var cells = new[]
                    {
                        row.Technician,
                        row.ClockInAt.ToOffset(NairobiOffset).ToString("dd MMM yyyy HH:mm"),
                        row.ClockOutAt?.ToOffset(NairobiOffset).ToString("dd MMM yyyy HH:mm") ?? "Shift open",
                        row.RecordedBy,
                        row.ClockOutRecordedBy ?? "—",
                        hours.ToString("0.00"),
                    };
                    foreach (var value in cells) table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(value);
                }
                if (entries.Count == 0) table.Cell().ColumnSpan(6).Padding(10).Text("No shifts recorded for this period.");
            });
            page.Footer().AlignRight().Text(text => { text.Span("Page "); text.CurrentPageNumber(); text.Span(" of "); text.TotalPages(); });
        }));

        await _audit.LogAsync(CurrentUserId, "REPORT_EXPORTED", "Report", CurrentUserId,
            $"Exported Technician timekeeping report from {fromDay:yyyy-MM-dd} to {toDay:yyyy-MM-dd} as PDF.", HttpContext.Connection.RemoteIpAddress?.ToString());
        return File(document.GeneratePdf(), "application/pdf", $"technician-timekeeping-{fromDay:yyyyMMdd}-{toDay:yyyyMMdd}.pdf");
    }

    private static (DateTimeOffset From, DateTimeOffset ToExclusive, DateOnly FromDay, DateOnly ToDay) GetRange(DateOnly? fromDate, DateOnly? toDate)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(NairobiOffset).DateTime);
        var fromDay = fromDate ?? today.AddDays(-30);
        var toDay = toDate ?? today;
        var from = new DateTimeOffset(fromDay.ToDateTime(TimeOnly.MinValue), NairobiOffset).ToUniversalTime();
        var toExclusive = new DateTimeOffset(toDay.AddDays(1).ToDateTime(TimeOnly.MinValue), NairobiOffset).ToUniversalTime();
        return (from, toExclusive, fromDay, toDay);
    }

    public sealed record ClockInRequest(Guid TechnicianId);
}
