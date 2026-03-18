using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.DTOs.Category;
using SmartSpend.Core.Models;
using SmartSpend.Infrastructure.Data;
using SmartSpend.Infrastructure.Services;

namespace SmartSpend.Tests.Services;

public class CategoryServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly CategoryService _categoryService;
    private readonly int _testUserId;

    public CategoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _categoryService = new CategoryService(_context);

        // Seed a test user
        var user = new User
        {
            Email = "test@example.com",
            PasswordHash = "hashedpassword",
            FullName = "Test User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        _context.SaveChanges();
        _testUserId = user.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region GetByUserIdAsync Tests

    [Fact]
    public async Task GetCategories_ReturnsDefaultAndUserCategories()
    {
        // Arrange - seed default categories and user-specific ones
        _context.Categories.AddRange(
            new Category { Name = "Food", Icon = "🍔", IsDefault = true, UserId = null },
            new Category { Name = "Transport", Icon = "🚗", IsDefault = true, UserId = null },
            new Category { Name = "My Custom", Icon = "⭐", IsDefault = false, UserId = _testUserId }
        );

        // Add another user's custom category (should NOT be returned)
        var otherUser = new User
        {
            Email = "other@example.com",
            PasswordHash = "hash",
            FullName = "Other",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        _context.Categories.Add(
            new Category { Name = "Other's Category", IsDefault = false, UserId = otherUser.Id }
        );
        await _context.SaveChangesAsync();

        // Act
        var results = await _categoryService.GetByUserIdAsync(_testUserId);

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain(c => c.Name == "Food" && c.IsDefault);
        results.Should().Contain(c => c.Name == "Transport" && c.IsDefault);
        results.Should().Contain(c => c.Name == "My Custom" && !c.IsDefault);
        results.Should().NotContain(c => c.Name == "Other's Category");
    }

    [Fact]
    public async Task GetCategories_NoCategories_ReturnsEmptyList()
    {
        // Act
        var results = await _categoryService.GetByUserIdAsync(_testUserId);

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateCategory_ValidRequest_ReturnsCategory()
    {
        // Arrange
        var request = new CreateCategoryRequest
        {
            Name = "Entertainment",
            Icon = "🎬"
        };

        // Act
        var result = await _categoryService.CreateAsync(_testUserId, request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("Entertainment");
        result.Icon.Should().Be("🎬");
        result.IsDefault.Should().BeFalse();

        // Verify persisted and linked to user
        var dbCategory = await _context.Categories.FindAsync(result.Id);
        dbCategory.Should().NotBeNull();
        dbCategory!.UserId.Should().Be(_testUserId);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateCategory_ValidRequest_ReturnsUpdatedCategory()
    {
        // Arrange
        var category = new Category
        {
            Name = "Old Name",
            Icon = "🔴",
            IsDefault = false,
            UserId = _testUserId
        };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateCategoryRequest
        {
            Name = "New Name",
            Icon = "🟢"
        };

        // Act
        var result = await _categoryService.UpdateAsync(_testUserId, category.Id, updateRequest);

        // Assert
        result.Name.Should().Be("New Name");
        result.Icon.Should().Be("🟢");
    }

    [Fact]
    public async Task UpdateCategory_NotOwner_ThrowsException()
    {
        // Arrange - create category owned by another user
        var otherUser = new User
        {
            Email = "other@example.com",
            PasswordHash = "hash",
            FullName = "Other",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        var category = new Category
        {
            Name = "Other's Category",
            IsDefault = false,
            UserId = otherUser.Id
        };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateCategoryRequest { Name = "Hacked" };

        // Act
        var act = async () => await _categoryService.UpdateAsync(_testUserId, category.Id, updateRequest);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You do not own this category");
    }

    [Fact]
    public async Task UpdateCategory_DefaultCategory_ThrowsException()
    {
        // Arrange
        var category = new Category
        {
            Name = "Food",
            IsDefault = true,
            UserId = null
        };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateCategoryRequest { Name = "Renamed" };

        // Act
        var act = async () => await _categoryService.UpdateAsync(_testUserId, category.Id, updateRequest);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You do not own this category");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteCategory_ValidId_RemovesCategory()
    {
        // Arrange
        var category = new Category
        {
            Name = "To Delete",
            IsDefault = false,
            UserId = _testUserId
        };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        // Act
        await _categoryService.DeleteAsync(_testUserId, category.Id);

        // Assert
        var deleted = await _context.Categories.FindAsync(category.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCategory_WithExpenses_ThrowsException()
    {
        // Arrange
        var category = new Category
        {
            Name = "Has Expenses",
            IsDefault = false,
            UserId = _testUserId
        };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        _context.Expenses.Add(new Expense
        {
            UserId = _testUserId,
            CategoryId = category.Id,
            Amount = 10.00m,
            ExpenseDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _categoryService.DeleteAsync(_testUserId, category.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot delete category with existing expenses");
    }

    [Fact]
    public async Task DeleteCategory_NotOwner_ThrowsException()
    {
        // Arrange
        var otherUser = new User
        {
            Email = "other@example.com",
            PasswordHash = "hash",
            FullName = "Other",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        var category = new Category
        {
            Name = "Not Mine",
            IsDefault = false,
            UserId = otherUser.Id
        };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _categoryService.DeleteAsync(_testUserId, category.Id);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You do not own this category");
    }

    #endregion
}
