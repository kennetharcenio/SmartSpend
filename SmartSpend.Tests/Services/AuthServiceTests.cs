using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartSpend.Core.DTOs.Auth;
using SmartSpend.Core.Settings;
using SmartSpend.Infrastructure.Data;
using SmartSpend.Infrastructure.Services;

namespace SmartSpend.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly AuthService _authService;
    private readonly JwtSettings _jwtSettings;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);

        _jwtSettings = new JwtSettings
        {
            SecretKey = "TestSecretKeyThatIsAtLeast32CharactersLong!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationInMinutes = 60
        };

        _authService = new AuthService(_context, Options.Create(_jwtSettings));
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region RegisterAsync Tests

    [Fact]
    public async Task RegisterAsync_ValidRequest_ReturnsAuthResponse()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
            FullName = "Test User"
        };

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be(request.Email);
        result.FullName.Should().Be(request.FullName);
        result.Token.Should().NotBeNullOrEmpty();
        result.UserId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsException()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "duplicate@example.com",
            Password = "Password123!",
            FullName = "Test User"
        };

        await _authService.RegisterAsync(request);

        // Act
        var act = async () => await _authService.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Email already registered");
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_HashesPassword()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "hash@example.com",
            Password = "Password123!",
            FullName = "Test User"
        };

        // Act
        await _authService.RegisterAsync(request);

        // Assert
        var user = await _context.Users.FirstAsync(u => u.Email == request.Email);
        user.PasswordHash.Should().NotBe(request.Password);
        BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash).Should().BeTrue();
    }

    #endregion

    #region LoginAsync Tests

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Email = "login@example.com",
            Password = "Password123!",
            FullName = "Test User"
        };
        await _authService.RegisterAsync(registerRequest);

        var loginRequest = new LoginRequest
        {
            Email = "login@example.com",
            Password = "Password123!"
        };

        // Act
        var result = await _authService.LoginAsync(loginRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(loginRequest.Email);
        result.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_InvalidEmail_ReturnsNull()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "Password123!"
        };

        // Act
        var result = await _authService.LoginAsync(loginRequest);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ReturnsNull()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Email = "wrongpass@example.com",
            Password = "Password123!",
            FullName = "Test User"
        };
        await _authService.RegisterAsync(registerRequest);

        var loginRequest = new LoginRequest
        {
            Email = "wrongpass@example.com",
            Password = "WrongPassword!"
        };

        // Act
        var result = await _authService.LoginAsync(loginRequest);

        // Assert
        result.Should().BeNull();
    }

    #endregion
}
