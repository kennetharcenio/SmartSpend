using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.DTOs.Webhooks;
using SmartSpend.Core.Models;
using SmartSpend.Infrastructure.Data;
using SmartSpend.Infrastructure.Services;

namespace SmartSpend.Tests.Services;

public class InsightServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly InsightService _service;
    private readonly int _userId;

    public InsightServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);

        var user = new User
        {
            Email = "test@example.com",
            PasswordHash = "hashed",
            FullName = "Test User"
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        _userId = user.Id;
        _service = new InsightService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CreateInsightAsync_ValidRequest_CreatesInsight()
    {
        var request = new CreateInsightRequest
        {
            UserId = _userId,
            MonthYear = "2026-03",
            InsightText = "You spent 30% more on food this month."
        };

        var result = await _service.CreateInsightAsync(request);

        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.UserId.Should().Be(_userId);
        result.MonthYear.Should().Be("2026-03");
        result.InsightText.Should().Be("You spent 30% more on food this month.");
    }

    [Fact]
    public async Task CreateInsightAsync_SetsGeneratedAt()
    {
        var request = new CreateInsightRequest
        {
            UserId = _userId,
            MonthYear = "2026-03",
            InsightText = "Test insight"
        };

        var before = DateTime.UtcNow;
        var result = await _service.CreateInsightAsync(request);
        var after = DateTime.UtcNow;

        result.GeneratedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task CreateInsightAsync_SetsExpiresAt30Days()
    {
        var request = new CreateInsightRequest
        {
            UserId = _userId,
            MonthYear = "2026-03",
            InsightText = "Test insight"
        };

        var result = await _service.CreateInsightAsync(request);

        result.ExpiresAt.Should().NotBeNull();
        result.ExpiresAt!.Value.Should().BeCloseTo(
            result.GeneratedAt.AddDays(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateInsightAsync_PersistsToDatabase()
    {
        var request = new CreateInsightRequest
        {
            UserId = _userId,
            MonthYear = "2026-03",
            InsightText = "Persisted insight"
        };

        var result = await _service.CreateInsightAsync(request);

        var saved = await _context.AIInsights.FindAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.InsightText.Should().Be("Persisted insight");
    }

    [Fact]
    public async Task CreateInsightAsync_InvalidUser_ThrowsException()
    {
        var request = new CreateInsightRequest
        {
            UserId = 999,
            MonthYear = "2026-03",
            InsightText = "Test"
        };

        var act = async () => await _service.CreateInsightAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*User not found*");
    }

    [Fact]
    public async Task CreateInsightAsync_ExistingMonthYear_UpdatesExisting()
    {
        var request1 = new CreateInsightRequest
        {
            UserId = _userId,
            MonthYear = "2026-03",
            InsightText = "Original insight"
        };
        await _service.CreateInsightAsync(request1);

        var request2 = new CreateInsightRequest
        {
            UserId = _userId,
            MonthYear = "2026-03",
            InsightText = "Updated insight"
        };
        var result = await _service.CreateInsightAsync(request2);

        result.InsightText.Should().Be("Updated insight");

        var count = await _context.AIInsights
            .CountAsync(i => i.UserId == _userId && i.MonthYear == "2026-03");
        count.Should().Be(1);
    }

    [Fact]
    public async Task CreateInsightAsync_DifferentMonthYear_CreatesSeparate()
    {
        await _service.CreateInsightAsync(new CreateInsightRequest
        {
            UserId = _userId,
            MonthYear = "2026-02",
            InsightText = "February insight"
        });

        await _service.CreateInsightAsync(new CreateInsightRequest
        {
            UserId = _userId,
            MonthYear = "2026-03",
            InsightText = "March insight"
        });

        var count = await _context.AIInsights.CountAsync(i => i.UserId == _userId);
        count.Should().Be(2);
    }
}
