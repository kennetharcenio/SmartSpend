namespace SmartSpend.Core.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public bool IsDefault { get; set; }
    public int? UserId { get; set; }

    public User? User { get; set; }
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
