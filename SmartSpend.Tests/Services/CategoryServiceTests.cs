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
    public async Task GetAllAsync_NoCustomCategories_ReturnsOnlyDefaults()
    {
        // Act
        var results = (await _categoryService.GetAllAsync(_userId)).ToList();

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(c => c.IsDefault);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOrderedByName()
    {
        // Arrange
        await _categoryService.CreateAsync(_userId, new CreateCategoryRequest { Name = "Zebra" });
        await _categoryService.CreateAsync(_userId, new CreateCategoryRequest { Name = "Alpha" });

        // Act
        var results = (await _categoryService.GetAllAsync(_userId)).ToList();

        // Assert
        var names = results.Select(c => c.Name).ToList();
        names.Should().BeInAscendingOrder();
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

    [Fact]
    public async Task CreateAsync_NameMatchesDefaultCategory_ThrowsException()
    {
        // Act - "Food" is a seeded default category
        var act = async () => await _categoryService.CreateAsync(_userId, new CreateCategoryRequest { Name = "Food" });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Category with this name already exists");
    }

    [Fact]
    public async Task CreateAsync_NullIcon_Succeeds()
    {
        // Arrange
        var request = new CreateCategoryRequest
        {
            Name = "NoIcon",
            Icon = null
        };

        // Act
        var result = await _categoryService.CreateAsync(_userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("NoIcon");
        result.Icon.Should().BeNull();
        result.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_PersistsToDatabase()
    {
        // Arrange
        var request = new CreateCategoryRequest { Name = "Persisted", Icon = "💾" };

        // Act
        var result = await _categoryService.CreateAsync(_userId, request);

        // Assert
        var category = await _context.Categories.FindAsync(result.Id);
        category.Should().NotBeNull();
        category!.Name.Should().Be("Persisted");
        category.UserId.Should().Be(_userId);
        category.IsDefault.Should().BeFalse();
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

    [Fact]
    public async Task UpdateAsync_PreservesIsDefaultAsFalse()
    {
        // Arrange
        var created = await _categoryService.CreateAsync(_userId, new CreateCategoryRequest
        {
            Name = "Custom",
            Icon = "⭐"
        });

        // Act
        var result = await _categoryService.UpdateAsync(_userId, created.Id, new UpdateCategoryRequest
        {
            Name = "Renamed Custom",
            Icon = "🌟"
        });

        // Assert
        result.Should().NotBeNull();
        result!.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangesToDatabase()
    {
        // Arrange
        var created = await _categoryService.CreateAsync(_userId, new CreateCategoryRequest
        {
            Name = "Original",
            Icon = "🔵"
        });

        // Act
        await _categoryService.UpdateAsync(_userId, created.Id, new UpdateCategoryRequest
        {
            Name = "Changed",
            Icon = "🔴"
        });

        // Assert
        var category = await _context.Categories.FindAsync(created.Id);
        category.Should().NotBeNull();
        category!.Name.Should().Be("Changed");
        category.Icon.Should().Be("🔴");
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

    [Fact]
    public async Task DeleteAsync_RemovesFromDatabase()
    {
        // Arrange
        var created = await _categoryService.CreateAsync(_userId, new CreateCategoryRequest { Name = "ToRemove" });

        // Act
        await _categoryService.DeleteAsync(_userId, created.Id);

        // Assert
        var category = await _context.Categories.FindAsync(created.Id);
        category.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersCategory_DoesNotDeleteCategory()
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
        await _categoryService.DeleteAsync(_userId, otherCategory.Id);

        // Assert - category should still exist in DB
        var category = await _context.Categories.FindAsync(otherCategory.Id);
        category.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_DefaultCategory_DoesNotRemoveFromDatabase()
    {
        // Arrange
        var defaultCategory = await _context.Categories.FirstAsync(c => c.IsDefault);

        // Act
        await _categoryService.DeleteAsync(_userId, defaultCategory.Id);

        // Assert
        var category = await _context.Categories.FindAsync(defaultCategory.Id);
        category.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_DoesNotAffectOtherCategories()
    {
        // Arrange
        var cat1 = await _categoryService.CreateAsync(_userId, new CreateCategoryRequest { Name = "Cat1" });
        var cat2 = await _categoryService.CreateAsync(_userId, new CreateCategoryRequest { Name = "Cat2" });

        // Act
        await _categoryService.DeleteAsync(_userId, cat1.Id);

        // Assert
        var remaining = (await _categoryService.GetAllAsync(_userId)).ToList();
        remaining.Should().Contain(c => c.Name == "Cat2");
        remaining.Should().NotContain(c => c.Name == "Cat1");
    }

    #endregion

    #region GetByIdAsync Edge Cases

    [Fact]
    public async Task GetByIdAsync_AfterUpdate_ReturnsUpdatedValues()
    {
        // Arrange
        var created = await _categoryService.CreateAsync(_userId, new CreateCategoryRequest
        {
            Name = "Original",
            Icon = "🔵"
        });

        await _categoryService.UpdateAsync(_userId, created.Id, new UpdateCategoryRequest
        {
            Name = "Updated",
            Icon = "🔴"
        });

        // Act
        var result = await _categoryService.GetByIdAsync(_userId, created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated");
        result.Icon.Should().Be("🔴");
    }

    [Fact]
    public async Task GetByIdAsync_AfterDelete_ReturnsNull()
    {
        // Arrange
        var created = await _categoryService.CreateAsync(_userId, new CreateCategoryRequest { Name = "Gone" });
        await _categoryService.DeleteAsync(_userId, created.Id);

        // Act
        var result = await _categoryService.GetByIdAsync(_userId, created.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion
}
