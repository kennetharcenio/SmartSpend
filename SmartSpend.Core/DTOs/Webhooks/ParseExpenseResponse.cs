namespace SmartSpend.Core.DTOs.Webhooks;

public class ParseExpenseResponse
{
    public decimal Amount { get; set; }
    public string Merchant { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public DateTime ExpenseDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; }
}
