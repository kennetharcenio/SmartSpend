namespace SmartSpend.Core.DTOs.Webhooks;

public class ExpenseSummaryResponse
{
    public int UserId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalSpent { get; set; }
    public Dictionary<string, decimal> CategoryBreakdown { get; set; } = new();
    public int ExpenseCount { get; set; }
}
