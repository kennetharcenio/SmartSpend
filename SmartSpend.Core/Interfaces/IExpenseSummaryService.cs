using SmartSpend.Core.DTOs.Webhooks;

namespace SmartSpend.Core.Interfaces;

public interface IExpenseSummaryService
{
    Task<ExpenseSummaryResponse> GetSummaryAsync(int userId, DateTime from, DateTime to);
}
