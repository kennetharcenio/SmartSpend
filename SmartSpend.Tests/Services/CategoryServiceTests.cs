using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.DTOs.Categories;
using SmartSpend.Core.Models;
using SmartSpend.Infrastructure.Data;
using SmartSpend.Infrastructure.Services;

namespace SmartSpend.Tests.Services;

public class CategoryServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly CategoryService _categoryService;
    private readonly int _userId;

    public CategoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);

        // Seed a user
        var user = new User
        {
            Email = "test@example.com",
            PasswordHash = "hashed",
            FullName = "Test User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        // Seed default categories
        _context.Categories.AddRange(
            new Category { Name = "Food", Icon = "🍔", IsDefault = true },
            new Category { Name = "Transport", Icon = "🚗", IsDefault = true }
        );
        _context.SaveChanges();

        _userId = user.Id;
        _categoryService = new CategoryService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsDefaultAndUserCategories()
    {
        // Arrange - create a custom category for the user
        await _categoryService.CreateAsync(_userId, new CreateCategoryRequest
        {
            Name = "Custom",
            Icon = "⭐"
        });

        // Act
        var results = (await _categoryService.GetAllAsync(_userId)).ToList();

        // Assert - should include 2 defaults + 1 custom
        results.Should().HaveCount(3);
        results.Should().Contain(c => c.Name == "Food" && c.IsDefault);
        results.Should().Contain(c => c.Name == "Transport" && c.IsDefault);
        results.Should().Contain(c => c.Name == "Custom" && !c.IsDefault);
    }

    [Fact]
    public async Task GetAllAsync_DoesNotReturnOtherUsersCategories()
    {
        // Arrange - create categories for two different users
        var otherUser = new User
        {
            Email = "other@example.com",
            PasswordHash = "hashed",
            FullName = "Other User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        await _categoryService.CreateAsync(_userId, new CreateCategoryRequest { Name = "MyCategory" });
        await _categoryService.CreateAsync(otherUser.Id, new CreateCategoryRequest { Name = "TheirCategory" });

        // Act
        var results = (await _categoryService.GetAllAsync(_userId)).ToList();

        // Assert
        results.Should().Contain(c => c.Name == "MyCategory");
        results.Should().NotContain(c => c.Name == "TheirCategory");
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_DefaultCategory_ReturnsCategory()
    {
        // Arrange
        var defaultCategory = await _context.Categories.FirstAsync(c => c.IsDefault);

        // Act
        var result = await _categoryService.GetByIdAsync(_userId, defaultCategory.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(defaultCategory.Name);
        result.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_UserCategory_ReturnsCategory()
    {
        // Arrange
        var created = await _categoryService.CreateAsync(_userId, new CreateCategoryRequest
        {
            Name = "Custom",
            Icon = "⭐"
        });

        // Act
        var result = await _categoryService.GetByIdAsync(_userId, created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Custom");
    }

    [Fact]
    public async Task GetByIdAsync_OtherUsersCategory_ReturnsNull()
    {
        // Arrange
        var otherUser = new User
        {
            Email = "other@example.com",
            PasswordHash = "hashed",
            FullName = "Other User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        var otherCategory = await _categoryService.CreateAsync(otherUser.Id, new CreateCategoryRequest
        {
            Name = "TheirCategory"
        });

        // Act
        var result = await _categoryService.GetByIdAsync(_userId, otherCategory.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        // Act
        var result = await _categoryService.GetByIdAsync(_userId, 999);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsCustomCategory()
    {
        // Arrange
        var request = new CreateCategoryRequest
        {
            Name = "Entertainment",
            Icon = "🎮"
        };

        // Act
        var result = await _categoryService.CreateAsync(_userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("Entertainment");
        result.Icon.Should().Be("🎮");
        result.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_DuplicateNameForUser_ThrowsException()
    {
        // Arrange
        await _categoryService.CreateAsync(_userId, new CreateCategoryRequest { Name = "Custom" });

        // Act
        var act = async () => await _categoryService.CreateAsync(_userId, new CreateCategoryRequest { Name = "Custom" });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Category with this name already exists");
    }

    [Fact]
    public async Task CreateAsync_SameNameAsDifferentUser_Succeeds()
    {
        // Arrange
        var otherUser = new User
        {
            Email = "other@example.com",
            PasswordHash = "hashed",
            FullName = "Other User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        await _categoryService.CreateAsync(otherUser.Id, new CreateCategoryRequest { Name = "Shared Name" });

        // Act
        var result = await _categoryService.CreateAsync(_userId, new CreateCategoryRequest { Name = "Shared Name" });

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Shared Name");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UserCategory_ReturnsUpdated()
    {
        // Arrange
        var created = await _categoryService.CreateAsync(_userId, new CreateCategoryRequest
        {
            Name = "Old Name",
            Icon = "🔵"
        });

        var updateRequest = new UpdateCategoryRequest
        {
            Name = "New Name",
            Icon = "🔴"
        };

        // Act
        var result = await _categoryService.UpdateAsync(_userId, created.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Name");
        result.Icon.Should().Be("🔴");
    }

    [Fact]
    public async Task UpdateAsync_DefaultCategory_ReturnsNull()
    {
        // Arrange
        var defaultCategory = await _context.Categories.FirstAsync(c => c.IsDefault);

        var updateRequest = new UpdateCategoryRequest
        {
            Name = "Hacked",
            Icon = "💀"
        };

        // Act
        var result = await _categoryService.UpdateAsync(_userId, defaultCategory.Id, updateRequest);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_OtherUsersCategory_ReturnsNull()
    {
        // Arrange
        var otherUser = new User
        {
            Email = "other@example.com",
            PasswordHash = "hashed",
            FullName = "Other User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        var otherCategory = await _categoryService.CreateAsync(otherUser.Id, new CreateCategoryRequest { Name = "Theirs" });

        // Act
        var result = await _categoryService.UpdateAsync(_userId, otherCategory.Id, new UpdateCategoryRequest { Name = "Stolen" });

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_NonExistent_ReturnsNull()
    {
        // Act
        var result = await _categoryService.UpdateAsync(_userId, 999, new UpdateCategoryRequest { Name = "X" });

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_UserCategory_ReturnsTrue()
    {
        // Arrange
        var created = await _categoryService.CreateAsync(_userId, new CreateCategoryRequest { Name = "ToDelete" });

        // Act
        var result = await _categoryService.DeleteAsync(_userId, created.Id);

        // Assert
        result.Should().BeTrue();
        (await _categoryService.GetByIdAsync(_userId, created.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_DefaultCategory_ReturnsFalse()
    {
        // Arrange
        var defaultCategory = await _context.Categories.FirstAsync(c => c.IsDefault);

        // Act
        var result = await _categoryService.DeleteAsync(_userId, defaultCategory.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersCategory_ReturnsFalse()
    {
        // Arrange
        var otherUser = new User
        {
            Email = "other@example.com",
            PasswordHash = "hashed",
            FullName = "Other User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        var otherCategory = await _categoryService.CreateAsync(otherUser.Id, new CreateCategoryRequest { Name = "Theirs" });

        // Act
        var result = await _categoryService.DeleteAsync(_userId, otherCategory.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_ReturnsFalse()
    {
        // Act
        var result = await _categoryService.DeleteAsync(_userId, 999);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
