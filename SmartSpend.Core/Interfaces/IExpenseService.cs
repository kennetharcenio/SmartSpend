using SmartSpend.Core.DTOs.Expense;

namespace SmartSpend.Core.Interfaces;

public interface IExpenseService
{
    Task<ExpenseResponse> CreateAsync(int userId, CreateExpenseRequest request);
    Task<List<ExpenseResponse>> GetByUserIdAsync(int userId);
    Task<ExpenseResponse?> GetByIdAsync(int userId, int expenseId);
    Task<ExpenseResponse> UpdateAsync(int userId, int expenseId, UpdateExpenseRequest request);
    Task DeleteAsync(int userId, int expenseId);
}
