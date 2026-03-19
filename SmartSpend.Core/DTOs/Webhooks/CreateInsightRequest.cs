using System.ComponentModel.DataAnnotations;

namespace SmartSpend.Core.DTOs.Webhooks;

public class CreateInsightRequest
{
    [Required]
    public int UserId { get; set; }

    [Required]
    [RegularExpression(@"^\d{4}-\d{2}$", ErrorMessage = "MonthYear must be in format 'YYYY-MM'")]
    public string MonthYear { get; set; } = string.Empty;

    [Required]
    public string InsightText { get; set; } = string.Empty;
}
