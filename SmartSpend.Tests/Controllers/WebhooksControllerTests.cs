using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartSpend.API.Controllers;
using SmartSpend.Core.DTOs.Webhooks;
using SmartSpend.Core.Interfaces;
using SmartSpend.Core.Models;

namespace SmartSpend.Tests.Controllers;

public class WebhooksControllerTests
{
    private readonly Mock<IExpenseParsingService> _parsingServiceMock;
    private readonly Mock<IExpenseSummaryService> _summaryServiceMock;
    private readonly Mock<IInsightService> _insightServiceMock;
    private readonly WebhooksController _controller;

    public WebhooksControllerTests()
    {
        _parsingServiceMock = new Mock<IExpenseParsingService>();
        _summaryServiceMock = new Mock<IExpenseSummaryService>();
        _insightServiceMock = new Mock<IInsightService>();

        _controller = new WebhooksController(
            _parsingServiceMock.Object,
            _summaryServiceMock.Object,
            _insightServiceMock.Object);
    }

    #region Parse Endpoint Tests

    [Fact]
    public async Task ParseExpense_ValidRequest_ReturnsOkWithParsedExpense()
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = "Spent $25 at McDonald's",
            UserId = 1
        };

        var expected = new ParseExpenseResponse
        {
            Amount = 25m,
            Merchant = "McDonald's",
            CategoryName = "Food",
            ExpenseDate = DateTime.UtcNow.Date,
            Description = "Spent $25 at McDonald's",
            Confidence = 0.8
        };

        _parsingServiceMock
            .Setup(s => s.ParseExpenseAsync(request))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.ParseExpense(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ParseExpenseResponse>().Subject;
        response.Amount.Should().Be(25m);
        response.Merchant.Should().Be("McDonald's");
        response.CategoryName.Should().Be("Food");
    }

    [Fact]
    public async Task ParseExpense_ServiceThrowsInvalidOperation_ReturnsBadRequest()
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = "no amount here",
            UserId = 1
        };

        _parsingServiceMock
            .Setup(s => s.ParseExpenseAsync(request))
            .ThrowsAsync(new InvalidOperationException("Could not extract amount from text"));

        // Act
        var result = await _controller.ParseExpense(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ParseExpense_NullRawText_ServiceThrows_ReturnsBadRequest()
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = null,
            UserId = 1
        };

        _parsingServiceMock
            .Setup(s => s.ParseExpenseAsync(request))
            .ThrowsAsync(new InvalidOperationException("RawText is required for expense parsing"));

        // Act
        var result = await _controller.ParseExpense(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ParseExpense_CallsServiceWithCorrectRequest()
    {
        // Arrange
        var request = new ParseExpenseRequest
        {
            RawText = "Test $10",
            UserId = 42
        };

        _parsingServiceMock
            .Setup(s => s.ParseExpenseAsync(It.IsAny<ParseExpenseRequest>()))
            .ReturnsAsync(new ParseExpenseResponse { Amount = 10m });

        // Act
        await _controller.ParseExpense(request);

        // Assert
        _parsingServiceMock.Verify(
            s => s.ParseExpenseAsync(It.Is<ParseExpenseRequest>(
                r => r.RawText == "Test $10" && r.UserId == 42)),
            Times.Once);
    }

    #endregion

    #region Summary Endpoint Tests

    [Fact]
    public async Task GetExpenseSummary_ValidRequest_ReturnsOkWithSummary()
    {
        // Arrange
        var from = new DateTime(2026, 3, 1);
        var to = new DateTime(2026, 3, 31);
        var userId = 1;

        var expected = new ExpenseSummaryResponse
        {
            UserId = userId,
            FromDate = from,
            ToDate = to,
            TotalSpent = 150m,
            ExpenseCount = 5,
            CategoryBreakdown = new Dictionary<string, decimal>
            {
                ["Food"] = 100m,
                ["Transport"] = 50m
            }
        };

        _summaryServiceMock
            .Setup(s => s.GetSummaryAsync(userId, from, to))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.GetExpenseSummary(userId, from, to);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ExpenseSummaryResponse>().Subject;
        response.TotalSpent.Should().Be(150m);
        response.ExpenseCount.Should().Be(5);
        response.CategoryBreakdown.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetExpenseSummary_NoExpenses_ReturnsOkWithZeroTotals()
    {
        // Arrange
        var from = new DateTime(2026, 3, 1);
        var to = new DateTime(2026, 3, 31);

        _summaryServiceMock
            .Setup(s => s.GetSummaryAsync(1, from, to))
            .ReturnsAsync(new ExpenseSummaryResponse
            {
                UserId = 1,
                FromDate = from,
                ToDate = to,
                TotalSpent = 0,
                ExpenseCount = 0,
                CategoryBreakdown = new()
            });

        // Act
        var result = await _controller.GetExpenseSummary(1, from, to);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ExpenseSummaryResponse>().Subject;
        response.TotalSpent.Should().Be(0);
        response.ExpenseCount.Should().Be(0);
    }

    [Fact]
    public async Task GetExpenseSummary_CallsServiceWithCorrectParameters()
    {
        // Arrange
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 31);
        var userId = 7;

        _summaryServiceMock
            .Setup(s => s.GetSummaryAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new ExpenseSummaryResponse());

        // Act
        await _controller.GetExpenseSummary(userId, from, to);

        // Assert
        _summaryServiceMock.Verify(
            s => s.GetSummaryAsync(7, from, to),
            Times.Once);
    }

    #endregion

    #region Insight Endpoint Tests

    [Fact]
    public async Task CreateInsight_ValidRequest_ReturnsOkWithId()
    {
        // Arrange
        var request = new CreateInsightRequest
        {
            UserId = 1,
            MonthYear = "2026-03",
            InsightText = "You spent 30% more on food this month."
        };

        _insightServiceMock
            .Setup(s => s.CreateInsightAsync(request))
            .ReturnsAsync(new AIInsight
            {
                Id = 42,
                UserId = 1,
                MonthYear = "2026-03",
                InsightText = "You spent 30% more on food this month.",
                GeneratedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            });

        // Act
        var result = await _controller.CreateInsight(request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.Value.Should().BeOfType<AIInsight>();
        var insight = (AIInsight)createdResult.Value!;
        insight.Id.Should().Be(42);
    }

    [Fact]
    public async Task CreateInsight_InvalidUser_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateInsightRequest
        {
            UserId = 999,
            MonthYear = "2026-03",
            InsightText = "Test"
        };

        _insightServiceMock
            .Setup(s => s.CreateInsightAsync(request))
            .ThrowsAsync(new InvalidOperationException("User not found"));

        // Act
        var result = await _controller.CreateInsight(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateInsight_CallsServiceWithCorrectRequest()
    {
        // Arrange
        var request = new CreateInsightRequest
        {
            UserId = 5,
            MonthYear = "2026-02",
            InsightText = "Test insight text"
        };

        _insightServiceMock
            .Setup(s => s.CreateInsightAsync(It.IsAny<CreateInsightRequest>()))
            .ReturnsAsync(new AIInsight { Id = 1 });

        // Act
        await _controller.CreateInsight(request);

        // Assert
        _insightServiceMock.Verify(
            s => s.CreateInsightAsync(It.Is<CreateInsightRequest>(
                r => r.UserId == 5 && r.MonthYear == "2026-02" && r.InsightText == "Test insight text")),
            Times.Once);
    }

    #endregion

    #region Controller Attribute Tests

    [Fact]
    public void WebhooksController_HasApiControllerAttribute()
    {
        // Assert
        typeof(WebhooksController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.ApiControllerAttribute), true)
            .Should().NotBeEmpty();
    }

    [Fact]
    public void WebhooksController_HasAuthorizeAttribute_WithApiKeyScheme()
    {
        // Assert
        var authorizeAttributes = typeof(WebhooksController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .ToList();

        authorizeAttributes.Should().NotBeEmpty();
        authorizeAttributes.Should().Contain(a => a.AuthenticationSchemes == "ApiKey");
    }

    [Fact]
    public void WebhooksController_HasRouteAttribute()
    {
        // Assert
        var routeAttributes = typeof(WebhooksController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RouteAttribute), true)
            .Cast<Microsoft.AspNetCore.Mvc.RouteAttribute>()
            .ToList();

        routeAttributes.Should().NotBeEmpty();
        routeAttributes.Should().Contain(a => a.Template == "api/webhooks");
    }

    #endregion
}
