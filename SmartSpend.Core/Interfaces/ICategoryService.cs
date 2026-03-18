using SmartSpend.Core.DTOs.Category;

namespace SmartSpend.Core.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryResponse>> GetByUserIdAsync(int userId);
    Task<CategoryResponse> CreateAsync(int userId, CreateCategoryRequest request);
    Task<CategoryResponse> UpdateAsync(int userId, int categoryId, UpdateCategoryRequest request);
    Task DeleteAsync(int userId, int categoryId);
}
