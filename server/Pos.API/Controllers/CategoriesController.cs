using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly PosDbContext _context;

    public CategoriesController(PosDbContext context)
    {
        _context = context;
    }

    /// <summary>Get all active categories</summary>
    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
        	{
            	Id = c.Id,
            	Name = c.Name,
            	Description = c.Description,
            	IsActive = c.IsActive,
            	ProductCount = c.Products.Count
        	})
        	.ToListAsync();
    	return Ok(categories);
    }

    /// <summary>Get a specific category by ID</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetCategory(Guid id)
    {
        var category = await _context.Categories
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive,
                ProductCount = c.Products.Count
            })
            .FirstOrDefaultAsync();
    
        if (category == null) return NotFound();
        return Ok(category);
    }

    /// <summary>Create a new category (Manager/Admin only)</summary>
    [HttpPost]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
    {
        // Validate category name is unique
        if (await _context.Categories.AnyAsync(c => c.Name == request.Name && c.IsActive))
            return BadRequest("A category with this name already exists");

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, MapToDto(category));
    }

    /// <summary>Update a category (Manager/Admin only)</summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateCategoryRequest request)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound();

        // Check if new name is unique (excluding current category)
        if (request.Name != category.Name && 
            await _context.Categories.AnyAsync(c => c.Name == request.Name && c.IsActive))
            return BadRequest("A category with this name already exists");

        category.Name = request.Name;
        category.Description = request.Description;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(MapToDto(category));
    }

    /// <summary>Delete a category (soft delete - Manager/Admin only)</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound();

        // Don't allow deletion if category has active products
        var activeProducts = await _context.Products
            .CountAsync(p => p.CategoryId == id && p.IsActive);
        if (activeProducts > 0)
            return BadRequest($"Cannot delete category with {activeProducts} active product(s)");

        category.IsActive = false;
        category.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }
	private CategoryDto MapToDto(Category c)
	{
    	return new CategoryDto
    	{
        	Id = c.Id,
        	Name = c.Name,
        	Description = c.Description,
        	IsActive = c.IsActive,
        	ProductCount = c.Products?.Count ?? 0
    	};
	}
}

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}