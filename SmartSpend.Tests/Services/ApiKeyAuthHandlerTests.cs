using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartSpend.Core.Settings;
using SmartSpend.Infrastructure.Auth;

namespace SmartSpend.Tests.Services;

public class ApiKeyAuthHandlerTests
{
    private const string ValidApiKey = "test-api-key-123";
    private const string SchemeName = "ApiKey";

    private static async Task<AuthenticateResult> RunHandlerAsync(string? apiKeyHeader)
    {
        var apiKeySettings = new ApiKeySettings
        {
            ValidKeys = new List<string> { ValidApiKey }
        };

        var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Setup(o => o.Get(SchemeName)).Returns(new AuthenticationSchemeOptions());

        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        var apiKeyOptions = new Mock<IOptions<ApiKeySettings>>();
        apiKeyOptions.Setup(o => o.Value).Returns(apiKeySettings);

        var handler = new ApiKeyAuthenticationHandler(
            options.Object,
            loggerFactory.Object,
            UrlEncoder.Default,
            apiKeyOptions.Object);

        var scheme = new AuthenticationScheme(SchemeName, SchemeName, typeof(ApiKeyAuthenticationHandler));
        var httpContext = new DefaultHttpContext();

        if (apiKeyHeader != null)
        {
            httpContext.Request.Headers["X-API-Key"] = apiKeyHeader;
        }

        await handler.InitializeAsync(scheme, httpContext);

        // Use reflection to call the protected HandleAuthenticateAsync
        var method = typeof(ApiKeyAuthenticationHandler)
            .GetMethod("HandleAuthenticateAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task<AuthenticateResult>)method!.Invoke(handler, null)!;
        return await task;
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ValidApiKey_ReturnsSuccess()
    {
        // Act
        var result = await RunHandlerAsync(ValidApiKey);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ValidApiKey_SetsExpectedClaims()
    {
        // Act
        var result = await RunHandlerAsync(ValidApiKey);

        // Assert
        result.Succeeded.Should().BeTrue();
        var nameClaim = result.Principal!.FindFirst(ClaimTypes.Name);
        nameClaim.Should().NotBeNull();
        nameClaim!.Value.Should().Be("n8n-webhook");

        var authMethodClaim = result.Principal.FindFirst(ClaimTypes.AuthenticationMethod);
        authMethodClaim.Should().NotBeNull();
        authMethodClaim!.Value.Should().Be("ApiKey");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_MissingApiKey_ReturnsFail()
    {
        // Act
        var result = await RunHandlerAsync(null);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("missing");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_InvalidApiKey_ReturnsFail()
    {
        // Act
        var result = await RunHandlerAsync("wrong-key");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_EmptyApiKey_ReturnsFail()
    {
        // Act
        var result = await RunHandlerAsync("");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid");
    }

    [Theory]
    [InlineData("test-api-key-123 ")] // trailing space
    [InlineData(" test-api-key-123")] // leading space
    [InlineData("TEST-API-KEY-123")] // wrong case
    public async Task HandleAuthenticateAsync_SimilarButWrongKey_ReturnsFail(string apiKey)
    {
        // Act
        var result = await RunHandlerAsync(apiKey);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ValidKey_SetsSchemeNameOnTicket()
    {
        // Act
        var result = await RunHandlerAsync(ValidApiKey);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Ticket!.AuthenticationScheme.Should().Be(SchemeName);
    }
}
