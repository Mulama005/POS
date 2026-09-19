using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/operating-expenses")]
[Authorize(Roles = "Manager,Admin")]
public class OperatingExpensesController : ControllerBase
{
    private readonly PosDbContext _context;
    public OperatingExpensesController(PosDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> List() => Ok(await _context.OperatingExpenses.AsNoTracking()
        .Where(x => x.IsActive)
        .OrderBy(x => x.Category).ThenBy(x => x.Name)
        .Select(x => new OperatingExpenseDto(x.Id, x.Name, x.Category, x.MonthlyAmount, x.IsActive))
        .ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveOperatingExpenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.MonthlyAmount < 0)
            return BadRequest("Provide an expense name and a non-negative monthly amount.");
        if (await _context.OperatingExpenses.AnyAsync(item => item.IsActive && item.Name.ToLower() == request.Name.Trim().ToLower()))
            return BadRequest("An active operating cost with this name already exists.");
        var item = new OperatingExpense { Name = request.Name.Trim(), Category = string.IsNullOrWhiteSpace(request.Category) ? "Occupancy" : request.Category.Trim(), MonthlyAmount = request.MonthlyAmount };
        _context.OperatingExpenses.Add(item);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(List), new OperatingExpenseDto(item.Id, item.Name, item.Category, item.MonthlyAmount, item.IsActive));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveOperatingExpenseRequest request)
    {
        var item = await _context.OperatingExpenses.FindAsync(id);
        if (item is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name) || request.MonthlyAmount < 0) return BadRequest("Provide an expense name and a non-negative monthly amount.");
        item.Name = request.Name.Trim(); item.Category = string.IsNullOrWhiteSpace(request.Category) ? "Occupancy" : request.Category.Trim(); item.MonthlyAmount = request.MonthlyAmount; item.IsActive = request.IsActive; item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new OperatingExpenseDto(item.Id, item.Name, item.Category, item.MonthlyAmount, item.IsActive));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _context.OperatingExpenses.FindAsync(id);
        if (item is null) return NotFound();
        item.IsActive = false; item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public record OperatingExpenseDto(Guid Id, string Name, string Category, decimal MonthlyAmount, bool IsActive);
public class SaveOperatingExpenseRequest { public string Name { get; set; } = string.Empty; public string Category { get; set; } = "Occupancy"; public decimal MonthlyAmount { get; set; } public bool IsActive { get; set; } = true; }
