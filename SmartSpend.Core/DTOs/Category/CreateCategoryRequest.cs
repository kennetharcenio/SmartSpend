using System.ComponentModel.DataAnnotations;

namespace SmartSpend.Core.DTOs.Category;

public class CreateCategoryRequest
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Icon { get; set; }
}
