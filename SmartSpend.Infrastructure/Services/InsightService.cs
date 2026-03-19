using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.DTOs.Webhooks;
using SmartSpend.Core.Interfaces;
using SmartSpend.Core.Models;
using SmartSpend.Infrastructure.Data;

namespace SmartSpend.Infrastructure.Services;

public class InsightService : IInsightService
{
    private readonly AppDbContext _context;

    public InsightService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AIInsight> CreateInsightAsync(CreateInsightRequest request)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == request.UserId);
        if (!userExists)
            throw new InvalidOperationException("User not found");

        // Check for existing insight for same user and month (upsert)
        var existing = await _context.AIInsights
            .FirstOrDefaultAsync(i => i.UserId == request.UserId && i.MonthYear == request.MonthYear);

        if (existing != null)
        {
            existing.InsightText = request.InsightText;
            existing.GeneratedAt = DateTime.UtcNow;
            existing.ExpiresAt = DateTime.UtcNow.AddDays(30);

            await _context.SaveChangesAsync();
            return existing;
        }

        var insight = new AIInsight
        {
            UserId = request.UserId,
            MonthYear = request.MonthYear,
            InsightText = request.InsightText,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _context.AIInsights.Add(insight);
        await _context.SaveChangesAsync();

        return insight;
    }
}
