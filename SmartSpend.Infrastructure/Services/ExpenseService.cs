using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.DTOs.Expense;
using SmartSpend.Core.Interfaces;
using SmartSpend.Infrastructure.Data;

namespace SmartSpend.Infrastructure.Services;

public class ExpenseService : IExpenseService
{
    private readonly AppDbContext _context;

    public ExpenseService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ExpenseResponse> CreateAsync(int userId, CreateExpenseRequest request)
    {
        var category = await _context.Categories.FindAsync(request.CategoryId);
        if (category == null)
            throw new ArgumentException("Category not found");

        if (category.UserId != null && category.UserId != userId)
            throw new ArgumentException("Category not found");

        var expense = new Core.Models.Expense
        {
            UserId = userId,
            CategoryId = request.CategoryId,
            Amount = request.Amount,
            Description = request.Description,
            Merchant = request.Merchant,
            ExpenseDate = request.ExpenseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        return MapToResponse(expense, category.Name);
    }

    public async Task<List<ExpenseResponse>> GetByUserIdAsync(int userId)
    {
        return await _context.Expenses
            .Where(e => e.UserId == userId)
            .Include(e => e.Category)
            .OrderByDescending(e => e.ExpenseDate)
            .Select(e => new ExpenseResponse
            {
                Id = e.Id,
                CategoryId = e.CategoryId,
                CategoryName = e.Category.Name,
                Amount = e.Amount,
                Description = e.Description,
                Merchant = e.Merchant,
                ExpenseDate = e.ExpenseDate,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<ExpenseResponse?> GetByIdAsync(int userId, int expenseId)
    {
        var expense = await _context.Expenses
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId);

        if (expense == null)
            return null;

        return MapToResponse(expense, expense.Category.Name);
    }

    public async Task<ExpenseResponse> UpdateAsync(int userId, int expenseId, UpdateExpenseRequest request)
    {
        var expense = await _context.Expenses
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.Id == expenseId);

        if (expense == null)
            throw new KeyNotFoundException("Expense not found");

        if (expense.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this expense");

        var category = await _context.Categories.FindAsync(request.CategoryId);
        if (category == null)
            throw new ArgumentException("Category not found");

        expense.CategoryId = request.CategoryId;
        expense.Amount = request.Amount;
        expense.Description = request.Description;
        expense.Merchant = request.Merchant;
        expense.ExpenseDate = request.ExpenseDate;
        expense.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToResponse(expense, category.Name);
    }

    public async Task DeleteAsync(int userId, int expenseId)
    {
        var expense = await _context.Expenses.FindAsync(expenseId);

        if (expense == null)
            throw new KeyNotFoundException("Expense not found");

        if (expense.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this expense");

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync();
    }

    private static ExpenseResponse MapToResponse(Core.Models.Expense expense, string categoryName)
    {
        return new ExpenseResponse
        {
            Id = expense.Id,
            CategoryId = expense.CategoryId,
            CategoryName = categoryName,
            Amount = expense.Amount,
            Description = expense.Description,
            Merchant = expense.Merchant,
            ExpenseDate = expense.ExpenseDate,
            CreatedAt = expense.CreatedAt
        };
    }
}
