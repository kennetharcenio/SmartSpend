using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartSpend.Core.DTOs.Webhooks;
using SmartSpend.Core.Models;
using SmartSpend.Core.Settings;
using SmartSpend.Infrastructure.Data;
using SmartSpend.Infrastructure.Services;

namespace SmartSpend.Tests.Services;

public class OpenAIExpenseParsingTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly int _userId;

    public OpenAIExpenseParsingTests()
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
            new Category { Name = "Food", Icon = "fork", IsDefault = true },
            new Category { Name = "Transport", Icon = "car", IsDefault = true },
            new Category { Name = "Shopping", Icon = "cart", IsDefault = true },
            new Category { Name = "Entertainment", Icon = "film", IsDefault = true },
            new Category { Name = "Other", Icon = "box", IsDefault = true }
        );
        _context.SaveChanges();

        _userId = user.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task ParseExpenseAsync_NoApiKey_FallsBackToRegex()
    {
        var settings = Options.Create(new OpenAISettings { ApiKey = "", Model = "gpt-4o" });
        var logger = Mock.Of<ILogger<ExpenseParsingService>>();
        var service = new ExpenseParsingService(_context, settings, logger, null);

        var request = new ParseExpenseRequest
        {
            RawText = "Spent $25.50 at McDonald's",
            UserId = _userId
        };

        var result = await service.ParseExpenseAsync(request);

        result.Amount.Should().Be(25.50m);
        result.Merchant.Should().Be("McDonald's");
        result.Confidence.Should().BeLessThan(0.95, "regex fallback should not produce 0.95 confidence");
    }

    [Fact]
    public async Task ParseExpenseAsync_NullApiKey_FallsBackToRegex()
    {
        var settings = Options.Create(new OpenAISettings { ApiKey = null!, Model = "gpt-4o" });
        var logger = Mock.Of<ILogger<ExpenseParsingService>>();
        var service = new ExpenseParsingService(_context, settings, logger, null);

        var request = new ParseExpenseRequest
        {
            RawText = "Uber ride $15",
            UserId = _userId
        };

        var result = await service.ParseExpenseAsync(request);

        result.Amount.Should().Be(15m);
        result.CategoryName.Should().Be("Transport");
    }

    [Fact]
    public void BuildPrompt_IncludesRawText()
    {
        var prompt = ExpenseParsingService.BuildPrompt("Lunch at Subway for $12");

        prompt.Should().Contain("Lunch at Subway for $12");
        prompt.Should().Contain("Parse this expense");
    }

    [Fact]
    public void BuildSystemMessage_IncludesCategories()
    {
        var categories = new List<string> { "Food", "Transport", "Shopping", "Other" };

        var message = ExpenseParsingService.BuildSystemMessage(categories);

        message.Should().Contain("Food");
        message.Should().Contain("Transport");
        message.Should().Contain("Shopping");
        message.Should().Contain("Other");
        message.Should().Contain("JSON");
    }

    [Fact]
    public void BuildSystemMessage_IncludesJsonFormat()
    {
        var categories = new List<string> { "Food" };

        var message = ExpenseParsingService.BuildSystemMessage(categories);

        message.Should().Contain("amount");
        message.Should().Contain("merchant");
        message.Should().Contain("categoryName");
        message.Should().Contain("expenseDate");
        message.Should().Contain("description");
    }

    [Fact]
    public void ParseOpenAIResponse_ValidJson_ReturnsCorrectResponse()
    {
        var json = """
        {
            "amount": 25.50,
            "merchant": "McDonald's",
            "categoryName": "Food",
            "expenseDate": "2026-03-15",
            "description": "Lunch at McDonald's"
        }
        """;

        var result = ExpenseParsingService.ParseOpenAIResponse(json, "Spent $25.50 at McDonald's");

        result.Amount.Should().Be(25.50m);
        result.Merchant.Should().Be("McDonald's");
        result.CategoryName.Should().Be("Food");
        result.ExpenseDate.Should().Be(new DateTime(2026, 3, 15));
        result.Description.Should().Be("Lunch at McDonald's");
        result.Confidence.Should().Be(0.95);
    }

    [Fact]
    public void ParseOpenAIResponse_MissingMerchant_DefaultsToEmpty()
    {
        var json = """
        {
            "amount": 10.00,
            "merchant": null,
            "categoryName": "Other",
            "expenseDate": "2026-03-15",
            "description": "Some expense"
        }
        """;

        var result = ExpenseParsingService.ParseOpenAIResponse(json, "test input");

        result.Merchant.Should().BeEmpty();
    }

    [Fact]
    public void ParseOpenAIResponse_MissingCategory_DefaultsToOther()
    {
        var json = """
        {
            "amount": 10.00,
            "merchant": "SomeStore",
            "categoryName": null,
            "expenseDate": "2026-03-15",
            "description": "Some expense"
        }
        """;

        var result = ExpenseParsingService.ParseOpenAIResponse(json, "test input");

        result.CategoryName.Should().Be("Other");
    }

    [Fact]
    public void ParseOpenAIResponse_AlwaysSetsConfidenceTo095()
    {
        var json = """
        {
            "amount": 5.00,
            "merchant": "",
            "categoryName": "Food",
            "expenseDate": "2026-03-15",
            "description": "food"
        }
        """;

        var result = ExpenseParsingService.ParseOpenAIResponse(json, "test");

        result.Confidence.Should().Be(0.95);
    }

    [Fact]
    public void ParseOpenAIResponse_InvalidJson_ThrowsException()
    {
        var act = () => ExpenseParsingService.ParseOpenAIResponse("not json", "test");

        act.Should().Throw<Exception>();
    }

    [Fact]
    public async Task ParseExpenseAsync_DefaultConstructor_StillWorksWithRegex()
    {
        // Test that the parameterless constructor (used by existing code) still works
        var service = new ExpenseParsingService(_context);

        var request = new ParseExpenseRequest
        {
            RawText = "Spent $25.50 at McDonald's",
            UserId = _userId
        };

        var result = await service.ParseExpenseAsync(request);

        result.Amount.Should().Be(25.50m);
        result.Merchant.Should().Be("McDonald's");
    }

    [Fact]
    public async Task ParseExpenseAsync_EmptyRawText_ThrowsException()
    {
        var settings = Options.Create(new OpenAISettings { ApiKey = "test-key", Model = "gpt-4o" });
        var logger = Mock.Of<ILogger<ExpenseParsingService>>();
        var service = new ExpenseParsingService(_context, settings, logger, null);

        var request = new ParseExpenseRequest
        {
            RawText = "",
            UserId = _userId
        };

        var act = async () => await service.ParseExpenseAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*RawText*");
    }
}
