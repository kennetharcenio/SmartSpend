using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.DTOs.Webhooks;
using SmartSpend.Core.Interfaces;
using SmartSpend.Infrastructure.Data;

namespace SmartSpend.Infrastructure.Services;

public class ExpenseSummaryService : IExpenseSummaryService
{
    private readonly AppDbContext _context;

    public ExpenseSummaryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ExpenseSummaryResponse> GetSummaryAsync(int userId, DateTime from, DateTime to)
    {
        var expenses = await _context.Expenses
            .Include(e => e.Category)
            .Where(e => e.UserId == userId
                && e.ExpenseDate >= from
                && e.ExpenseDate <= to)
            .ToListAsync();

        var categoryBreakdown = expenses
            .GroupBy(e => e.Category.Name)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        return new ExpenseSummaryResponse
        {
            UserId = userId,
            FromDate = from,
            ToDate = to,
            TotalSpent = expenses.Sum(e => e.Amount),
            CategoryBreakdown = categoryBreakdown,
            ExpenseCount = expenses.Count
        };
    }
}
