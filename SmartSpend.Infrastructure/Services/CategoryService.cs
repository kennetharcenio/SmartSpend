using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.DTOs.Categories;
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

    public async Task<IEnumerable<CategoryResponse>> GetAllAsync(int userId)
    {
        return await _context.Categories
            .Where(c => c.IsDefault || c.UserId == userId)
            .OrderBy(c => c.Name)
            .Select(c => MapToResponse(c))
            .ToListAsync();
    }

    public async Task<CategoryResponse?> GetByIdAsync(int userId, int categoryId)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && (c.IsDefault || c.UserId == userId));

        return category is null ? null : MapToResponse(category);
    }

    public async Task<CategoryResponse> CreateAsync(int userId, CreateCategoryRequest request)
    {
        var exists = await _context.Categories
            .AnyAsync(c => c.Name == request.Name && (c.UserId == userId || c.IsDefault));

        if (exists)
            throw new InvalidOperationException("Category with this name already exists");

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

    public async Task<CategoryResponse?> UpdateAsync(int userId, int categoryId, UpdateCategoryRequest request)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId && !c.IsDefault);

        if (category is null)
            return null;

        category.Name = request.Name;
        category.Icon = request.Icon;

        await _context.SaveChangesAsync();

        return MapToResponse(category);
    }

    public async Task<bool> DeleteAsync(int userId, int categoryId)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId && !c.IsDefault);

        if (category is null)
            return false;

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return true;
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
