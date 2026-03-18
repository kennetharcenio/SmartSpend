using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.DTOs.Expense;
using SmartSpend.Core.Interfaces;
using SmartSpend.Core.Models;
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
        if (!await _context.Categories.AnyAsync(c => c.Id == request.CategoryId))
        {
            throw new InvalidOperationException("Category not found");
        }

        var expense = new Expense
        {
            UserId = userId,
            CategoryId = request.CategoryId,
            Amount = request.Amount,
            Description = request.Description,
            Merchant = request.Merchant,
            ExpenseDate = request.ExpenseDate,
            IsAIParsed = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        return MapToResponse(expense);
    }

    public async Task<ExpenseResponse?> GetByIdAsync(int userId, int expenseId)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId);

        return expense is null ? null : MapToResponse(expense);
    }

    public async Task<IEnumerable<ExpenseResponse>> GetAllAsync(int userId)
    {
        var expenses = await _context.Expenses
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync();

        return expenses.Select(MapToResponse);
    }

    public async Task<ExpenseResponse?> UpdateAsync(int userId, int expenseId, UpdateExpenseRequest request)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId);

        if (expense is null)
        {
            return null;
        }

        if (!await _context.Categories.AnyAsync(c => c.Id == request.CategoryId))
        {
            throw new InvalidOperationException("Category not found");
        }

        expense.Amount = request.Amount;
        expense.CategoryId = request.CategoryId;
        expense.Description = request.Description;
        expense.Merchant = request.Merchant;
        expense.ExpenseDate = request.ExpenseDate;
        expense.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToResponse(expense);
    }

    public async Task<bool> DeleteAsync(int userId, int expenseId)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId);

        if (expense is null)
        {
            return false;
        }

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync();

        return true;
    }

    private static ExpenseResponse MapToResponse(Expense expense)
    {
        return new ExpenseResponse
        {
            Id = expense.Id,
            UserId = expense.UserId,
            CategoryId = expense.CategoryId,
            Amount = expense.Amount,
            Description = expense.Description,
            Merchant = expense.Merchant,
            ExpenseDate = expense.ExpenseDate,
            IsAIParsed = expense.IsAIParsed,
            CreatedAt = expense.CreatedAt,
            UpdatedAt = expense.UpdatedAt
        };
    }
}
