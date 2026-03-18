namespace SmartSpend.Core.DTOs.Expenses;

public class ExpenseResponse
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? Merchant { get; set; }
    public DateTime ExpenseDate { get; set; }
    public bool IsAIParsed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
