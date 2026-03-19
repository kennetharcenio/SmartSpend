using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.Models;
using SmartSpend.Infrastructure.Data;
using SmartSpend.Infrastructure.Services;

namespace SmartSpend.Tests.Services;

public class ExpenseSummaryServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ExpenseSummaryService _service;
    private readonly int _userId;
    private readonly int _categoryId1;
    private readonly int _categoryId2;

    public ExpenseSummaryServiceTests()
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

        var cat1 = new Category { Name = "Food", Icon = "🍔", IsDefault = true };
        var cat2 = new Category { Name = "Transport", Icon = "🚗", IsDefault = true };
        _context.Categories.AddRange(cat1, cat2);
        _context.SaveChanges();

        _userId = user.Id;
        _categoryId1 = cat1.Id;
        _categoryId2 = cat2.Id;

        _service = new ExpenseSummaryService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetSummaryAsync_WithExpenses_ReturnsTotalSpent()
    {
        SeedExpenses();

        var result = await _service.GetSummaryAsync(_userId,
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        result.TotalSpent.Should().Be(60m); // 10 + 20 + 30
    }

    [Fact]
    public async Task GetSummaryAsync_WithExpenses_ReturnsCorrectCount()
    {
        SeedExpenses();

        var result = await _service.GetSummaryAsync(_userId,
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        result.ExpenseCount.Should().Be(3);
    }

    [Fact]
    public async Task GetSummaryAsync_WithExpenses_ReturnsCategoryBreakdown()
    {
        SeedExpenses();

        var result = await _service.GetSummaryAsync(_userId,
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        result.CategoryBreakdown.Should().ContainKey("Food").WhoseValue.Should().Be(30m);
        result.CategoryBreakdown.Should().ContainKey("Transport").WhoseValue.Should().Be(30m);
    }

    [Fact]
    public async Task GetSummaryAsync_SetsDateRange()
    {
        var from = new DateTime(2026, 3, 1);
        var to = new DateTime(2026, 3, 31);

        var result = await _service.GetSummaryAsync(_userId, from, to);

        result.UserId.Should().Be(_userId);
        result.FromDate.Should().Be(from);
        result.ToDate.Should().Be(to);
    }

    [Fact]
    public async Task GetSummaryAsync_NoExpenses_ReturnsZeroTotals()
    {
        var result = await _service.GetSummaryAsync(_userId,
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        result.TotalSpent.Should().Be(0);
        result.ExpenseCount.Should().Be(0);
        result.CategoryBreakdown.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSummaryAsync_FiltersOutOfRangeExpenses()
    {
        SeedExpenses();

        // Add an expense outside the range
        _context.Expenses.Add(new Expense
        {
            UserId = _userId,
            CategoryId = _categoryId1,
            Amount = 100m,
            ExpenseDate = new DateTime(2026, 4, 15),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();

        var result = await _service.GetSummaryAsync(_userId,
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        result.TotalSpent.Should().Be(60m);
        result.ExpenseCount.Should().Be(3);
    }

    [Fact]
    public async Task GetSummaryAsync_OnlyReturnsUserExpenses()
    {
        SeedExpenses();

        // Add expense for another user
        var otherUser = new User
        {
            Email = "other@example.com",
            PasswordHash = "hashed",
            FullName = "Other User"
        };
        _context.Users.Add(otherUser);
        _context.SaveChanges();

        _context.Expenses.Add(new Expense
        {
            UserId = otherUser.Id,
            CategoryId = _categoryId1,
            Amount = 500m,
            ExpenseDate = new DateTime(2026, 3, 10),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();

        var result = await _service.GetSummaryAsync(_userId,
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        result.TotalSpent.Should().Be(60m);
    }

    private void SeedExpenses()
    {
        _context.Expenses.AddRange(
            new Expense
            {
                UserId = _userId,
                CategoryId = _categoryId1,
                Amount = 10m,
                ExpenseDate = new DateTime(2026, 3, 5),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Expense
            {
                UserId = _userId,
                CategoryId = _categoryId1,
                Amount = 20m,
                ExpenseDate = new DateTime(2026, 3, 10),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Expense
            {
                UserId = _userId,
                CategoryId = _categoryId2,
                Amount = 30m,
                ExpenseDate = new DateTime(2026, 3, 15),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );
        _context.SaveChanges();
    }
}
