using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.DTOs.Webhooks;
using SmartSpend.Core.Models;
using SmartSpend.Infrastructure.Data;
using SmartSpend.Infrastructure.Services;

namespace SmartSpend.Tests.Services;

public class ExpenseParsingEdgeCaseTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ExpenseParsingService _service;
    private readonly int _userId;

    public ExpenseParsingEdgeCaseTests()
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

        _context.Categories.AddRange(
            new Category { Name = "Food", Icon = "F", IsDefault = true },
            new Category { Name = "Transport", Icon = "T", IsDefault = true },
            new Category { Name = "Shopping", Icon = "S", IsDefault = true },
            new Category { Name = "Entertainment", Icon = "E", IsDefault = true },
            new Category { Name = "Other", Icon = "O", IsDefault = true }
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
    public async Task ParseExpenseAsync_EmptyString_ThrowsException()
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = "",
            UserId = _userId
        };

        // Act
        var act = async () => await _service.ParseExpenseAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*RawText*");
    }

    [Fact]
    public async Task ParseExpenseAsync_WhitespaceOnly_ThrowsException()
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = "   ",
            UserId = _userId
        };

        // Act
        var act = async () => await _service.ParseExpenseAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*RawText*");
    }

    [Fact]
    public async Task ParseExpenseAsync_LargeAmount_ExtractsCorrectly()
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = "Paid $9999.99 at store",
            UserId = _userId
        };

        // Act
        var result = await _service.ParseExpenseAsync(request);

        // Assert
        result.Amount.Should().Be(9999.99m);
    }

    [Fact]
    public async Task ParseExpenseAsync_WholeNumberAmount_ExtractsCorrectly()
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = "Spent $100 at the mall",
            UserId = _userId
        };

        // Act
        var result = await _service.ParseExpenseAsync(request);

        // Assert
        result.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task ParseExpenseAsync_ShoppingKeyword_MapsShoppingCategory()
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = "$50 at Amazon store",
            UserId = _userId
        };

        // Act
        var result = await _service.ParseExpenseAsync(request);

        // Assert
        result.CategoryName.Should().Be("Shopping");
    }

    [Fact]
    public async Task ParseExpenseAsync_EntertainmentKeyword_MapsEntertainmentCategory()
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = "Movie ticket $15",
            UserId = _userId
        };

        // Act
        var result = await _service.ParseExpenseAsync(request);

        // Assert
        result.CategoryName.Should().Be("Entertainment");
    }

    [Fact]
    public async Task ParseExpenseAsync_USDateFormat_ExtractsDate()
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = "Paid $10 on 03/15/2026",
            UserId = _userId
        };

        // Act
        var result = await _service.ParseExpenseAsync(request);

        // Assert
        result.ExpenseDate.Should().Be(new DateTime(2026, 3, 15));
    }

    [Fact]
    public async Task ParseExpenseAsync_MultipleAmounts_ExtractsFirst()
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = "Paid $10 and $20 at store",
            UserId = _userId
        };

        // Act
        var result = await _service.ParseExpenseAsync(request);

        // Assert
        result.Amount.Should().Be(10m);
    }

    [Theory]
    [InlineData("Bought grocery items $45", "Food")]
    [InlineData("Gas station $30", "Transport")]
    [InlineData("Netflix subscription $15", "Entertainment")]
    [InlineData("Walmart purchase $60", "Shopping")]
    public async Task ParseExpenseAsync_VariousKeywords_MapsCorrectCategory(string rawText, string expectedCategory)
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = rawText,
            UserId = _userId
        };

        // Act
        var result = await _service.ParseExpenseAsync(request);

        // Assert
        result.CategoryName.Should().Be(expectedCategory);
    }

    [Fact]
    public async Task ParseExpenseAsync_HighConfidence_WhenAllFieldsPresent()
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = "Spent $25.50 at McDonald's on 2026-03-15",
            UserId = _userId
        };

        // Act
        var result = await _service.ParseExpenseAsync(request);

        // Assert
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.7);
    }

    [Fact]
    public async Task ParseExpenseAsync_LowerConfidence_WhenNoMerchant()
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = "$5 for something",
            UserId = _userId
        };

        // Act
        var result = await _service.ParseExpenseAsync(request);

        // Assert
        // No merchant extracted -> lower confidence
        result.Confidence.Should().BeLessThan(0.9);
    }

    [Fact]
    public async Task ParseExpenseAsync_DescriptionIsRawText()
    {
        // Arrange
        var rawText = "Spent $25.50 at McDonald's";
        var request = new ParseExpenseRequest
        {
            RawText = rawText,
            UserId = _userId
        };

        // Act
        var result = await _service.ParseExpenseAsync(request);

        // Assert
        result.Description.Should().Be(rawText);
    }
}
