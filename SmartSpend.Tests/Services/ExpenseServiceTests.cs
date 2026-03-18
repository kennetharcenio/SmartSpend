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
    private readonly int _testUserId;
    private readonly int _testCategoryId;

    public ExpenseServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _expenseService = new ExpenseService(_context);

        // Seed a test user and category
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

        var category = new Category
        {
            Name = "Food",
            Icon = "🍔",
            IsDefault = true,
            UserId = null
        };
        _context.Categories.Add(category);
        _context.SaveChanges();
        _testCategoryId = category.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateExpense_ValidRequest_ReturnsExpense()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            CategoryId = _testCategoryId,
            Amount = 25.50m,
            Description = "Lunch at cafe",
            Merchant = "Cafe ABC",
            ExpenseDate = DateTime.UtcNow.Date
        };

        // Act
        var result = await _expenseService.CreateAsync(_testUserId, request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.CategoryId.Should().Be(_testCategoryId);
        result.CategoryName.Should().Be("Food");
        result.Amount.Should().Be(25.50m);
        result.Description.Should().Be("Lunch at cafe");
        result.Merchant.Should().Be("Cafe ABC");
        result.ExpenseDate.Should().Be(request.ExpenseDate);

        // Verify persisted in database
        var dbExpense = await _context.Expenses.FindAsync(result.Id);
        dbExpense.Should().NotBeNull();
        dbExpense!.UserId.Should().Be(_testUserId);
    }

    [Fact]
    public async Task CreateExpense_InvalidCategory_ThrowsException()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            CategoryId = 9999,
            Amount = 10.00m,
            Description = "Test",
            ExpenseDate = DateTime.UtcNow.Date
        };

        // Act
        var act = async () => await _expenseService.CreateAsync(_testUserId, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Category not found");
    }

    [Fact]
    public async Task CreateExpense_OtherUsersCategory_ThrowsException()
    {
        // Arrange - create another user's custom category
        var otherUser = new User
        {
            Email = "other@example.com",
            PasswordHash = "hash",
            FullName = "Other User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        var otherCategory = new Category
        {
            Name = "Other's Category",
            IsDefault = false,
            UserId = otherUser.Id
        };
        _context.Categories.Add(otherCategory);
        await _context.SaveChangesAsync();

        var request = new CreateExpenseRequest
        {
            CategoryId = otherCategory.Id,
            Amount = 10.00m,
            ExpenseDate = DateTime.UtcNow.Date
        };

        // Act
        var act = async () => await _expenseService.CreateAsync(_testUserId, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Category not found");
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Fact]
    public async Task GetExpenses_ReturnsOnlyUserExpenses()
    {
        // Arrange - create expenses for two different users
        var otherUser = new User
        {
            Email = "other@example.com",
            PasswordHash = "hash",
            FullName = "Other User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        _context.Expenses.AddRange(
            new Expense
            {
                UserId = _testUserId,
                CategoryId = _testCategoryId,
                Amount = 10.00m,
                ExpenseDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Expense
            {
                UserId = _testUserId,
                CategoryId = _testCategoryId,
                Amount = 20.00m,
                ExpenseDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Expense
            {
                UserId = otherUser.Id,
                CategoryId = _testCategoryId,
                Amount = 99.00m,
                ExpenseDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );
        await _context.SaveChangesAsync();

        // Act
        var results = await _expenseService.GetByUserIdAsync(_testUserId);

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.Amount.Should().NotBe(99.00m));
    }

    [Fact]
    public async Task GetExpenses_NoExpenses_ReturnsEmptyList()
    {
        // Act
        var results = await _expenseService.GetByUserIdAsync(_testUserId);

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateExpense_ValidRequest_ReturnsUpdatedExpense()
    {
        // Arrange
        var expense = new Expense
        {
            UserId = _testUserId,
            CategoryId = _testCategoryId,
            Amount = 10.00m,
            Description = "Original",
            ExpenseDate = DateTime.UtcNow.Date,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateExpenseRequest
        {
            CategoryId = _testCategoryId,
            Amount = 30.00m,
            Description = "Updated lunch",
            Merchant = "New Place",
            ExpenseDate = DateTime.UtcNow.Date.AddDays(-1)
        };

        // Act
        var result = await _expenseService.UpdateAsync(_testUserId, expense.Id, updateRequest);

        // Assert
        result.Amount.Should().Be(30.00m);
        result.Description.Should().Be("Updated lunch");
        result.Merchant.Should().Be("New Place");
        result.ExpenseDate.Should().Be(updateRequest.ExpenseDate);
    }

    [Fact]
    public async Task UpdateExpense_NotOwner_ThrowsException()
    {
        // Arrange - create expense owned by another user
        var otherUser = new User
        {
            Email = "other@example.com",
            PasswordHash = "hash",
            FullName = "Other User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        var expense = new Expense
        {
            UserId = otherUser.Id,
            CategoryId = _testCategoryId,
            Amount = 50.00m,
            ExpenseDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateExpenseRequest
        {
            CategoryId = _testCategoryId,
            Amount = 100.00m,
            ExpenseDate = DateTime.UtcNow
        };

        // Act
        var act = async () => await _expenseService.UpdateAsync(_testUserId, expense.Id, updateRequest);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You do not own this expense");
    }

    [Fact]
    public async Task UpdateExpense_NonExistent_ThrowsException()
    {
        // Arrange
        var updateRequest = new UpdateExpenseRequest
        {
            CategoryId = _testCategoryId,
            Amount = 10.00m,
            ExpenseDate = DateTime.UtcNow
        };

        // Act
        var act = async () => await _expenseService.UpdateAsync(_testUserId, 9999, updateRequest);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Expense not found");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteExpense_ValidId_RemovesExpense()
    {
        // Arrange
        var expense = new Expense
        {
            UserId = _testUserId,
            CategoryId = _testCategoryId,
            Amount = 15.00m,
            ExpenseDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        // Act
        await _expenseService.DeleteAsync(_testUserId, expense.Id);

        // Assert
        var deleted = await _context.Expenses.FindAsync(expense.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteExpense_NotOwner_ThrowsException()
    {
        // Arrange
        var otherUser = new User
        {
            Email = "other@example.com",
            PasswordHash = "hash",
            FullName = "Other User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        var expense = new Expense
        {
            UserId = otherUser.Id,
            CategoryId = _testCategoryId,
            Amount = 50.00m,
            ExpenseDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _expenseService.DeleteAsync(_testUserId, expense.Id);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You do not own this expense");
    }

    [Fact]
    public async Task DeleteExpense_NonExistent_ThrowsException()
    {
        // Act
        var act = async () => await _expenseService.DeleteAsync(_testUserId, 9999);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Expense not found");
    }

    #endregion
}
