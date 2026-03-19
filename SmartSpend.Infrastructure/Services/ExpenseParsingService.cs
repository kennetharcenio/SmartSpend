using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.DTOs.Webhooks;
using SmartSpend.Core.Interfaces;
using SmartSpend.Infrastructure.Data;

namespace SmartSpend.Infrastructure.Services;

public class ExpenseParsingService : IExpenseParsingService
{
    private readonly AppDbContext _context;

    private static readonly Dictionary<string, string[]> CategoryKeywords = new()
    {
        ["Food"] = ["lunch", "dinner", "breakfast", "coffee", "restaurant", "cafe", "pizza", "burger", "subway", "mcdonald", "starbucks", "eat", "food", "grocery", "groceries"],
        ["Transport"] = ["uber", "lyft", "taxi", "cab", "gas", "fuel", "parking", "bus", "train", "metro", "ride", "transport", "drive"],
        ["Shopping"] = ["amazon", "walmart", "target", "store", "shop", "buy", "purchase", "mall", "clothes", "clothing"],
        ["Entertainment"] = ["movie", "netflix", "spotify", "game", "concert", "show", "theater", "theatre", "ticket", "entertainment"]
    };

    public ExpenseParsingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ParseExpenseResponse> ParseExpenseAsync(ParseExpenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RawText))
            throw new InvalidOperationException("RawText is required for expense parsing");

        var text = request.RawText;
        var amount = ExtractAmount(text);
        var merchant = ExtractMerchant(text);
        var date = ExtractDate(text);
        var categoryName = await MapCategoryAsync(text, request.UserId);
        var confidence = CalculateConfidence(amount, merchant, date);

        return new ParseExpenseResponse
        {
            Amount = amount,
            Merchant = merchant,
            CategoryName = categoryName,
            ExpenseDate = date,
            Description = text,
            Confidence = confidence
        };
    }

    private static decimal ExtractAmount(string text)
    {
        // Match patterns like $25.50, $25, 25.50, etc.
        var match = Regex.Match(text, @"\$(\d+(?:\.\d{1,2})?)");
        if (match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            return amount;

        // Try without dollar sign - look for standalone numbers
        match = Regex.Match(text, @"(?:^|\s)(\d+(?:\.\d{1,2})?)(?:\s|$)");
        if (match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
            return amount;

        throw new InvalidOperationException("Could not extract amount from text");
    }

    private static string ExtractMerchant(string text)
    {
        // Try "at <merchant>" pattern
        var match = Regex.Match(text, @"\bat\s+([A-Z][A-Za-z'']+(?:\s+[A-Z][A-Za-z'']+)*)", RegexOptions.None);
        if (match.Success)
            return match.Groups[1].Value;

        // Try "at <merchant>" with lowercase
        match = Regex.Match(text, @"\bat\s+(\w+(?:'s)?)", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value;

        return string.Empty;
    }

    private static DateTime ExtractDate(string text)
    {
        // ISO format: 2026-03-15
        var match = Regex.Match(text, @"(\d{4}-\d{2}-\d{2})");
        if (match.Success && DateTime.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        // US format: 03/15/2026
        match = Regex.Match(text, @"(\d{1,2}/\d{1,2}/\d{4})");
        if (match.Success && DateTime.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date;

        // "on March 15" etc.
        match = Regex.Match(text, @"on\s+((?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2})", RegexOptions.IgnoreCase);
        if (match.Success && DateTime.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date;

        return DateTime.UtcNow.Date;
    }

    private async Task<string> MapCategoryAsync(string text, int userId)
    {
        var textLower = text.ToLowerInvariant();

        // Check keyword matches
        foreach (var (category, keywords) in CategoryKeywords)
        {
            if (keywords.Any(k => textLower.Contains(k)))
            {
                // Verify category exists in DB
                var exists = await _context.Categories
                    .AnyAsync(c => c.Name == category && (c.IsDefault || c.UserId == userId));
                if (exists)
                    return category;
            }
        }

        // Default to "Other"
        var otherExists = await _context.Categories
            .AnyAsync(c => c.Name == "Other" && (c.IsDefault || c.UserId == userId));

        return otherExists ? "Other" : "Other";
    }

    private static double CalculateConfidence(decimal amount, string merchant, DateTime date)
    {
        var confidence = 0.0;

        if (amount > 0) confidence += 0.4;
        if (!string.IsNullOrEmpty(merchant)) confidence += 0.3;
        if (date != DateTime.UtcNow.Date) confidence += 0.2;
        else confidence += 0.1; // Still some confidence even with default date

        return Math.Min(confidence, 1.0);
    }
}
