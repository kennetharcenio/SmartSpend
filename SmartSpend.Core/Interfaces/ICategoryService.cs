using SmartSpend.Core.DTOs.Categories;

namespace SmartSpend.Core.Interfaces;

public interface ICategoryService
{
    Task<IEnumerable<CategoryResponse>> GetAllAsync(int userId);
    Task<CategoryResponse?> GetByIdAsync(int userId, int categoryId);
    Task<CategoryResponse> CreateAsync(int userId, CreateCategoryRequest request);
    Task<CategoryResponse?> UpdateAsync(int userId, int categoryId, UpdateCategoryRequest request);
    Task<bool> DeleteAsync(int userId, int categoryId);
}
