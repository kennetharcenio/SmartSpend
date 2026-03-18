using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.DTOs.Expenses;
using SmartSpend.Core.Models;
using SmartSpend.Infrastructure.Data;
using SmartSpend.Infrastructure.Services;

namespace SmartSpend.Tests.Services;

public class ExpenseServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ExpenseService _expenseService;
    private readonly int _userId;
    private readonly int _categoryId;

    public ExpenseServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);

        // Seed a user and category for tests
        var user = new User
        {
            Email = "test@example.com",
            PasswordHash = "hashed",
            FullName = "Test User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var category = new Category
        {
            Name = "Food",
            Icon = "🍔",
            IsDefault = true
        };
        _context.Categories.Add(category);
        _context.SaveChanges();

        _userId = user.Id;
        _categoryId = category.Id;

        _expenseService = new ExpenseService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsExpenseResponse()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 25.50m,
            Description = "Lunch",
            Merchant = "McDonald's",
            ExpenseDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var result = await _expenseService.CreateAsync(_userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.CategoryId.Should().Be(_categoryId);
        result.CategoryName.Should().Be("Food");
        result.Amount.Should().Be(25.50m);
        result.Description.Should().Be("Lunch");
        result.Merchant.Should().Be("McDonald's");
        result.IsAIParsed.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_InvalidCategory_ThrowsException()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            CategoryId = 999,
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow
        };

        // Act
        var act = async () => await _expenseService.CreateAsync(_userId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Category not found");
    }

    [Fact]
    public async Task CreateAsync_SetsCreatedAtAndUpdatedAt()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow
        };

        // Act
        var before = DateTime.UtcNow;
        var result = await _expenseService.CreateAsync(_userId, request);
        var after = DateTime.UtcNow;

        // Assert
        result.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        result.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsUserExpensesOnly()
    {
        // Arrange - create expenses for our user
        await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow
        });
        await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 20m,
            ExpenseDate = DateTime.UtcNow
        });

        // Create another user with an expense
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
        await _expenseService.CreateAsync(otherUser.Id, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 30m,
            ExpenseDate = DateTime.UtcNow
        });

        // Act
        var results = (await _expenseService.GetAllAsync(_userId)).ToList();

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(e => e.Amount == 10m || e.Amount == 20m);
    }

    [Fact]
    public async Task GetAllAsync_NoExpenses_ReturnsEmptyList()
    {
        // Act
        var results = await _expenseService.GetAllAsync(_userId);

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingExpense_ReturnsExpenseResponse()
    {
        // Arrange
        var created = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 15m,
            Description = "Coffee",
            ExpenseDate = DateTime.UtcNow
        });

        // Act
        var result = await _expenseService.GetByIdAsync(_userId, created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.Amount.Should().Be(15m);
        result.Description.Should().Be("Coffee");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentExpense_ReturnsNull()
    {
        // Act
        var result = await _expenseService.GetByIdAsync(_userId, 999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_OtherUsersExpense_ReturnsNull()
    {
        // Arrange
        var created = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 15m,
            ExpenseDate = DateTime.UtcNow
        });

        // Act - try to access with different userId
        var result = await _expenseService.GetByIdAsync(_userId + 100, created.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ValidRequest_ReturnsUpdatedExpense()
    {
        // Arrange
        var created = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            Description = "Old",
            ExpenseDate = DateTime.UtcNow
        });

        var updateRequest = new UpdateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 20m,
            Description = "Updated",
            Merchant = "New Merchant",
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var result = await _expenseService.UpdateAsync(_userId, created.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Amount.Should().Be(20m);
        result.Description.Should().Be("Updated");
        result.Merchant.Should().Be("New Merchant");
        result.UpdatedAt.Should().BeAfter(result.CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentExpense_ReturnsNull()
    {
        // Arrange
        var updateRequest = new UpdateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 20m,
            ExpenseDate = DateTime.UtcNow
        };

        // Act
        var result = await _expenseService.UpdateAsync(_userId, 999, updateRequest);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_OtherUsersExpense_ReturnsNull()
    {
        // Arrange
        var created = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow
        });

        var updateRequest = new UpdateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 20m,
            ExpenseDate = DateTime.UtcNow
        };

        // Act
        var result = await _expenseService.UpdateAsync(_userId + 100, created.Id, updateRequest);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ExistingExpense_ReturnsTrue()
    {
        // Arrange
        var created = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow
        });

        // Act
        var result = await _expenseService.DeleteAsync(_userId, created.Id);

        // Assert
        result.Should().BeTrue();
        (await _expenseService.GetByIdAsync(_userId, created.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentExpense_ReturnsFalse()
    {
        // Act
        var result = await _expenseService.DeleteAsync(_userId, 999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersExpense_ReturnsFalse()
    {
        // Arrange
        var created = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow
        });

        // Act
        var result = await _expenseService.DeleteAsync(_userId + 100, created.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
