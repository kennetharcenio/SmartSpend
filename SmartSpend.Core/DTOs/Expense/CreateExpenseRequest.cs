using System.ComponentModel.DataAnnotations;

namespace SmartSpend.Core.DTOs.Expense;

public class CreateExpenseRequest
{
    [Required]
    public int CategoryId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
    public decimal Amount { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Merchant { get; set; }

    [Required]
    public DateTime ExpenseDate { get; set; }
}
