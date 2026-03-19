using System.ComponentModel.DataAnnotations;

namespace SmartSpend.Core.DTOs.Webhooks;

public class ParseExpenseRequest
{
    public string? RawText { get; set; }
    public string? ImageUrl { get; set; }

    [Required]
    public int UserId { get; set; }
}
