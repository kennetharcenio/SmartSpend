using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.DTOs.Webhooks;
using SmartSpend.Core.Models;
using SmartSpend.Infrastructure.Data;
using SmartSpend.Infrastructure.Services;

namespace SmartSpend.Tests.Services;

public class ExpenseParsingServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ExpenseParsingService _service;
    private readonly int _userId;

    public ExpenseParsingServiceTests()
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

        // Seed categories
        _context.Categories.AddRange(
            new Category { Name = "Food", Icon = "🍔", IsDefault = true },
            new Category { Name = "Transport", Icon = "🚗", IsDefault = true },
            new Category { Name = "Shopping", Icon = "🛒", IsDefault = true },
            new Category { Name = "Entertainment", Icon = "🎬", IsDefault = true },
            new Category { Name = "Other", Icon = "📦", IsDefault = true }
        );
        _context.SaveChanges();

        _userId = user.Id;
        _service = new ExpenseParsingService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task ParseExpenseAsync_SimpleAmountAndMerchant_ExtractsCorrectly()
    {
        var request = new ParseExpenseRequest
        {
            RawText = "Spent $25.50 at McDonald's",
            UserId = _userId
        };

        var result = await _service.ParseExpenseAsync(request);

        result.Amount.Should().Be(25.50m);
        result.Merchant.Should().Be("McDonald's");
    }

    [Fact]
    public async Task ParseExpenseAsync_WithDate_ExtractsDate()
    {
        var request = new ParseExpenseRequest
        {
            RawText = "Paid $10 at Starbucks on 2026-03-15",
            UserId = _userId
        };

        var result = await _service.ParseExpenseAsync(request);

        result.Amount.Should().Be(10m);
        result.ExpenseDate.Should().Be(new DateTime(2026, 3, 15));
    }

    [Fact]
    public async Task ParseExpenseAsync_NoDate_DefaultsToToday()
    {
        var request = new ParseExpenseRequest
        {
            RawText = "Spent $5 at cafe",
            UserId = _userId
        };

        var result = await _service.ParseExpenseAsync(request);

        result.ExpenseDate.Date.Should().Be(DateTime.UtcNow.Date);
    }

    [Fact]
    public async Task ParseExpenseAsync_FoodKeyword_MapsFoodCategory()
    {
        var request = new ParseExpenseRequest
        {
            RawText = "Lunch at Subway for $12",
            UserId = _userId
        };

        var result = await _service.ParseExpenseAsync(request);

        result.CategoryName.Should().Be("Food");
    }

    [Fact]
    public async Task ParseExpenseAsync_TransportKeyword_MapsTransportCategory()
    {
        var request = new ParseExpenseRequest
        {
            RawText = "Uber ride $15",
            UserId = _userId
        };

        var result = await _service.ParseExpenseAsync(request);

        result.CategoryName.Should().Be("Transport");
    }

    [Fact]
    public async Task ParseExpenseAsync_NoMatchingCategory_DefaultsToOther()
    {
        var request = new ParseExpenseRequest
        {
            RawText = "Paid $50 for random stuff",
            UserId = _userId
        };

        var result = await _service.ParseExpenseAsync(request);

        result.CategoryName.Should().Be("Other");
    }

    [Fact]
    public async Task ParseExpenseAsync_NoAmount_ThrowsException()
    {
        var request = new ParseExpenseRequest
        {
            RawText = "Bought something at the store",
            UserId = _userId
        };

        var act = async () => await _service.ParseExpenseAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*amount*");
    }

    [Fact]
    public async Task ParseExpenseAsync_NullRawText_ThrowsException()
    {
        var request = new ParseExpenseRequest
        {
            RawText = null,
            UserId = _userId
        };

        var act = async () => await _service.ParseExpenseAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*RawText*");
    }

    [Fact]
    public async Task ParseExpenseAsync_SetsConfidenceScore()
    {
        var request = new ParseExpenseRequest
        {
            RawText = "Spent $25.50 at McDonald's on 2026-03-15",
            UserId = _userId
        };

        var result = await _service.ParseExpenseAsync(request);

        result.Confidence.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task ParseExpenseAsync_SetsDescription()
    {
        var request = new ParseExpenseRequest
        {
            RawText = "Spent $25.50 at McDonald's",
            UserId = _userId
        };

        var result = await _service.ParseExpenseAsync(request);

        result.Description.Should().NotBeNullOrWhiteSpace();
    }
}
