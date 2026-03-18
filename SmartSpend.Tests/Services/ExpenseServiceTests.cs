using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartSpend.Core.DTOs.Expense;
using SmartSpend.Core.Models;
using SmartSpend.Infrastructure.Data;
using SmartSpend.Infrastructure.Services;

namespace SmartSpend.Tests.Services;

public class ExpenseServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ExpenseService _expenseService;

    public ExpenseServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _expenseService = new ExpenseService(_context);

        SeedData();
    }

    private void SeedData()
    {
        var user = new User
        {
            Id = 1,
            Email = "test@example.com",
            PasswordHash = "hash",
            FullName = "Test User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var otherUser = new User
        {
            Id = 2,
            Email = "other@example.com",
            PasswordHash = "hash",
            FullName = "Other User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var category = new Category
        {
            Id = 1,
            Name = "Food",
            IsDefault = true
        };

        _context.Users.AddRange(user, otherUser);
        _context.Categories.Add(category);
        _context.SaveChanges();
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
            Amount = 25.50m,
            CategoryId = 1,
            Description = "Lunch",
            Merchant = "Restaurant",
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var result = await _expenseService.CreateAsync(1, request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.UserId.Should().Be(1);
        result.Amount.Should().Be(25.50m);
        result.CategoryId.Should().Be(1);
        result.Description.Should().Be("Lunch");
        result.Merchant.Should().Be("Restaurant");
        result.IsAIParsed.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsToDatabase()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            Amount = 10.00m,
            CategoryId = 1,
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var result = await _expenseService.CreateAsync(1, request);

        // Assert
        var expense = await _context.Expenses.FindAsync(result.Id);
        expense.Should().NotBeNull();
        expense!.UserId.Should().Be(1);
        expense.Amount.Should().Be(10.00m);
    }

    [Fact]
    public async Task CreateAsync_InvalidCategory_ThrowsException()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            Amount = 10.00m,
            CategoryId = 999,
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var act = async () => await _expenseService.CreateAsync(1, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Category not found");
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingExpense_ReturnsExpenseResponse()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            Amount = 15.00m,
            CategoryId = 1,
            Description = "Coffee",
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };
        var created = await _expenseService.CreateAsync(1, request);

        // Act
        var result = await _expenseService.GetByIdAsync(1, created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.Amount.Should().Be(15.00m);
        result.Description.Should().Be("Coffee");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Act
        var result = await _expenseService.GetByIdAsync(1, 999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_OtherUsersExpense_ReturnsNull()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            Amount = 20.00m,
            CategoryId = 1,
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };
        var created = await _expenseService.CreateAsync(1, request);

        // Act - user 2 tries to access user 1's expense
        var result = await _expenseService.GetByIdAsync(2, created.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_UserHasExpenses_ReturnsAllUserExpenses()
    {
        // Arrange
        var request1 = new CreateExpenseRequest
        {
            Amount = 10.00m,
            CategoryId = 1,
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };
        var request2 = new CreateExpenseRequest
        {
            Amount = 20.00m,
            CategoryId = 1,
            ExpenseDate = new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc)
        };
        await _expenseService.CreateAsync(1, request1);
        await _expenseService.CreateAsync(1, request2);

        // Act
        var results = await _expenseService.GetAllAsync(1);

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_NoExpenses_ReturnsEmptyList()
    {
        // Act
        var results = await _expenseService.GetAllAsync(1);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_DoesNotReturnOtherUsersExpenses()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            Amount = 10.00m,
            CategoryId = 1,
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };
        await _expenseService.CreateAsync(1, request);

        // Act - user 2 gets their expenses
        var results = await _expenseService.GetAllAsync(2);

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ValidRequest_ReturnsUpdatedExpense()
    {
        // Arrange
        var createRequest = new CreateExpenseRequest
        {
            Amount = 10.00m,
            CategoryId = 1,
            Description = "Original",
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };
        var created = await _expenseService.CreateAsync(1, createRequest);

        var updateRequest = new UpdateExpenseRequest
        {
            Amount = 25.00m,
            CategoryId = 1,
            Description = "Updated",
            Merchant = "New Merchant",
            ExpenseDate = new DateTime(2026, 3, 19, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var result = await _expenseService.UpdateAsync(1, created.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Amount.Should().Be(25.00m);
        result.Description.Should().Be("Updated");
        result.Merchant.Should().Be("New Merchant");
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        var updateRequest = new UpdateExpenseRequest
        {
            Amount = 10.00m,
            CategoryId = 1,
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var result = await _expenseService.UpdateAsync(1, 999, updateRequest);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_OtherUsersExpense_ReturnsNull()
    {
        // Arrange
        var createRequest = new CreateExpenseRequest
        {
            Amount = 10.00m,
            CategoryId = 1,
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };
        var created = await _expenseService.CreateAsync(1, createRequest);

        var updateRequest = new UpdateExpenseRequest
        {
            Amount = 50.00m,
            CategoryId = 1,
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act - user 2 tries to update user 1's expense
        var result = await _expenseService.UpdateAsync(2, created.Id, updateRequest);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_InvalidCategory_ThrowsException()
    {
        // Arrange
        var createRequest = new CreateExpenseRequest
        {
            Amount = 10.00m,
            CategoryId = 1,
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };
        var created = await _expenseService.CreateAsync(1, createRequest);

        var updateRequest = new UpdateExpenseRequest
        {
            Amount = 10.00m,
            CategoryId = 999,
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var act = async () => await _expenseService.UpdateAsync(1, created.Id, updateRequest);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Category not found");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ExistingExpense_ReturnsTrue()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            Amount = 10.00m,
            CategoryId = 1,
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };
        var created = await _expenseService.CreateAsync(1, request);

        // Act
        var result = await _expenseService.DeleteAsync(1, created.Id);

        // Assert
        result.Should().BeTrue();
        var expense = await _context.Expenses.FindAsync(created.Id);
        expense.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsFalse()
    {
        // Act
        var result = await _expenseService.DeleteAsync(1, 999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersExpense_ReturnsFalse()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            Amount = 10.00m,
            CategoryId = 1,
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };
        var created = await _expenseService.CreateAsync(1, request);

        // Act - user 2 tries to delete user 1's expense
        var result = await _expenseService.DeleteAsync(2, created.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
