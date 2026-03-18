using SmartSpend.Core.DTOs.Expense;

namespace SmartSpend.Core.Interfaces;

public interface IExpenseService
{
    Task<ExpenseResponse> CreateAsync(int userId, CreateExpenseRequest request);
    Task<ExpenseResponse?> GetByIdAsync(int userId, int expenseId);
    Task<IEnumerable<ExpenseResponse>> GetAllAsync(int userId);
    Task<ExpenseResponse?> UpdateAsync(int userId, int expenseId, UpdateExpenseRequest request);
    Task<bool> DeleteAsync(int userId, int expenseId);
}
