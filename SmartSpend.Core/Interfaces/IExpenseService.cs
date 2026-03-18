using SmartSpend.Core.DTOs.Expenses;

namespace SmartSpend.Core.Interfaces;

public interface IExpenseService
{
    Task<IEnumerable<ExpenseResponse>> GetAllAsync(int userId);
    Task<ExpenseResponse?> GetByIdAsync(int userId, int expenseId);
    Task<ExpenseResponse> CreateAsync(int userId, CreateExpenseRequest request);
    Task<ExpenseResponse?> UpdateAsync(int userId, int expenseId, UpdateExpenseRequest request);
    Task<bool> DeleteAsync(int userId, int expenseId);
}
