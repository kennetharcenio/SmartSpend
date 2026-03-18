using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.DTOs.Category;
using SmartSpend.Core.Interfaces;
using SmartSpend.Core.Models;
using SmartSpend.Infrastructure.Data;

namespace SmartSpend.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _context;

    public CategoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<CategoryResponse>> GetByUserIdAsync(int userId)
    {
        return await _context.Categories
            .Where(c => c.IsDefault || c.UserId == userId)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse
            {
                Id = c.Id,
                Name = c.Name,
                Icon = c.Icon,
                IsDefault = c.IsDefault
            })
            .ToListAsync();
    }

    public async Task<CategoryResponse> CreateAsync(int userId, CreateCategoryRequest request)
    {
        var category = new Category
        {
            Name = request.Name,
            Icon = request.Icon,
            IsDefault = false,
            UserId = userId
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return MapToResponse(category);
    }

    public async Task<CategoryResponse> UpdateAsync(int userId, int categoryId, UpdateCategoryRequest request)
    {
        var category = await _context.Categories.FindAsync(categoryId);

        if (category == null)
            throw new KeyNotFoundException("Category not found");

        if (category.IsDefault || category.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this category");

        category.Name = request.Name;
        category.Icon = request.Icon;

        await _context.SaveChangesAsync();

        return MapToResponse(category);
    }

    public async Task DeleteAsync(int userId, int categoryId)
    {
        var category = await _context.Categories
            .Include(c => c.Expenses)
            .FirstOrDefaultAsync(c => c.Id == categoryId);

        if (category == null)
            throw new KeyNotFoundException("Category not found");

        if (category.IsDefault || category.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this category");

        if (category.Expenses.Any())
            throw new InvalidOperationException("Cannot delete category with existing expenses");

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
    }

    private static CategoryResponse MapToResponse(Category category)
    {
        return new CategoryResponse
        {
            Id = category.Id,
            Name = category.Name,
            Icon = category.Icon,
            IsDefault = category.IsDefault
        };
    }
}
