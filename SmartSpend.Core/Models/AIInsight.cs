namespace SmartSpend.Core.Models;

public class AIInsight
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string MonthYear { get; set; } = string.Empty; // Format: "2024-01"
    public string InsightText { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    public User User { get; set; } = null!;
}
