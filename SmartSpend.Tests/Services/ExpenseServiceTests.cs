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
    public async Task CreateAsync_ValidRequest_PersistsToDatabase()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10.00m,
            ExpenseDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var result = await _expenseService.CreateAsync(_userId, request);

        // Assert
        var expense = await _context.Expenses.FindAsync(result.Id);
        expense.Should().NotBeNull();
        expense!.UserId.Should().Be(_userId);
        expense.Amount.Should().Be(10.00m);
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

    [Fact]
    public async Task CreateAsync_NullOptionalFields_Succeeds()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 50m,
            Description = null,
            Merchant = null,
            ExpenseDate = DateTime.UtcNow
        };

        // Act
        var result = await _expenseService.CreateAsync(_userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Description.Should().BeNull();
        result.Merchant.Should().BeNull();
        result.Amount.Should().Be(50m);
    }

    [Fact]
    public async Task CreateAsync_VeryLargeAmount_Succeeds()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 999999999.99m,
            ExpenseDate = DateTime.UtcNow
        };

        // Act
        var result = await _expenseService.CreateAsync(_userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Amount.Should().Be(999999999.99m);
    }

    [Fact]
    public async Task CreateAsync_SmallAmount_Succeeds()
    {
        // Arrange
        var request = new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 0.01m,
            ExpenseDate = DateTime.UtcNow
        };

        // Act
        var result = await _expenseService.CreateAsync(_userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Amount.Should().Be(0.01m);
    }

    [Fact]
    public async Task CreateAsync_MapsCorrectCategoryName()
    {
        // Arrange
        var newCategory = new Category { Name = "Entertainment", Icon = "🎬", IsDefault = false, UserId = _userId };
        _context.Categories.Add(newCategory);
        await _context.SaveChangesAsync();

        var request = new CreateExpenseRequest
        {
            CategoryId = newCategory.Id,
            Amount = 15m,
            ExpenseDate = DateTime.UtcNow
        };

        // Act
        var result = await _expenseService.CreateAsync(_userId, request);

        // Assert
        result.CategoryName.Should().Be("Entertainment");
        result.CategoryId.Should().Be(newCategory.Id);
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

    [Fact]
    public async Task GetAllAsync_ReturnsExpensesOrderedByDateDescending()
    {
        // Arrange
        await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            ExpenseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 30m,
            ExpenseDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 20m,
            ExpenseDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // Act
        var results = (await _expenseService.GetAllAsync(_userId)).ToList();

        // Assert
        results.Should().HaveCount(3);
        results[0].Amount.Should().Be(30m); // March (most recent)
        results[1].Amount.Should().Be(20m); // February
        results[2].Amount.Should().Be(10m); // January (oldest)
    }

    [Fact]
    public async Task GetAllAsync_MultipleUsers_IsolatesData()
    {
        // Arrange
        var user2 = new User
        {
            Email = "user2@example.com",
            PasswordHash = "hashed",
            FullName = "User 2",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var user3 = new User
        {
            Email = "user3@example.com",
            PasswordHash = "hashed",
            FullName = "User 3",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.AddRange(user2, user3);
        await _context.SaveChangesAsync();

        await _expenseService.CreateAsync(_userId, new CreateExpenseRequest { CategoryId = _categoryId, Amount = 10m, ExpenseDate = DateTime.UtcNow });
        await _expenseService.CreateAsync(user2.Id, new CreateExpenseRequest { CategoryId = _categoryId, Amount = 20m, ExpenseDate = DateTime.UtcNow });
        await _expenseService.CreateAsync(user3.Id, new CreateExpenseRequest { CategoryId = _categoryId, Amount = 30m, ExpenseDate = DateTime.UtcNow });

        // Act & Assert
        (await _expenseService.GetAllAsync(_userId)).Should().HaveCount(1);
        (await _expenseService.GetAllAsync(user2.Id)).Should().HaveCount(1);
        (await _expenseService.GetAllAsync(user3.Id)).Should().HaveCount(1);
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

    [Fact]
    public async Task UpdateAsync_InvalidCategory_ThrowsException()
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
            CategoryId = 999,
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow
        };

        // Act
        var act = async () => await _expenseService.UpdateAsync(_userId, created.Id, updateRequest);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Category not found");
    }

    [Fact]
    public async Task UpdateAsync_PreservesCreatedAtTimestamp()
    {
        // Arrange
        var created = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow
        });
        var originalCreatedAt = created.CreatedAt;

        var updateRequest = new UpdateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 99m,
            ExpenseDate = DateTime.UtcNow
        };

        // Act
        var result = await _expenseService.UpdateAsync(_userId, created.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result!.CreatedAt.Should().Be(originalCreatedAt);
        result.UpdatedAt.Should().BeOnOrAfter(originalCreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_ChangesCategory_UpdatesCategoryName()
    {
        // Arrange
        var newCategory = new Category { Name = "Transport", Icon = "🚗", IsDefault = true };
        _context.Categories.Add(newCategory);
        await _context.SaveChangesAsync();

        var created = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow
        });
        created.CategoryName.Should().Be("Food");

        var updateRequest = new UpdateExpenseRequest
        {
            CategoryId = newCategory.Id,
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow
        };

        // Act
        var result = await _expenseService.UpdateAsync(_userId, created.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result!.CategoryId.Should().Be(newCategory.Id);
        result.CategoryName.Should().Be("Transport");
    }

    [Fact]
    public async Task UpdateAsync_AllFields_UpdatesEverything()
    {
        // Arrange
        var created = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            Description = "Old desc",
            Merchant = "Old merchant",
            ExpenseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        var updateRequest = new UpdateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 99.99m,
            Description = "New desc",
            Merchant = "New merchant",
            ExpenseDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var result = await _expenseService.UpdateAsync(_userId, created.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Amount.Should().Be(99.99m);
        result.Description.Should().Be("New desc");
        result.Merchant.Should().Be("New merchant");
        result.ExpenseDate.Should().Be(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
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

    [Fact]
    public async Task DeleteAsync_RemovesFromDatabase()
    {
        // Arrange
        var created = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow
        });

        // Act
        await _expenseService.DeleteAsync(_userId, created.Id);

        // Assert
        var expense = await _context.Expenses.FindAsync(created.Id);
        expense.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersExpense_DoesNotDeleteExpense()
    {
        // Arrange
        var created = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow
        });

        // Act
        await _expenseService.DeleteAsync(_userId + 100, created.Id);

        // Assert - expense should still exist
        var expense = await _context.Expenses.FindAsync(created.Id);
        expense.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_DoesNotAffectOtherExpenses()
    {
        // Arrange
        var expense1 = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow
        });
        var expense2 = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 20m,
            ExpenseDate = DateTime.UtcNow
        });

        // Act
        await _expenseService.DeleteAsync(_userId, expense1.Id);

        // Assert
        var remaining = (await _expenseService.GetAllAsync(_userId)).ToList();
        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().Be(expense2.Id);
    }

    #endregion

    #region GetByIdAsync Edge Cases

    [Fact]
    public async Task GetByIdAsync_AfterUpdate_ReturnsUpdatedValues()
    {
        // Arrange
        var created = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            Description = "Original",
            ExpenseDate = DateTime.UtcNow
        });

        await _expenseService.UpdateAsync(_userId, created.Id, new UpdateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 50m,
            Description = "Updated",
            ExpenseDate = DateTime.UtcNow
        });

        // Act
        var result = await _expenseService.GetByIdAsync(_userId, created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Amount.Should().Be(50m);
        result.Description.Should().Be("Updated");
    }

    [Fact]
    public async Task GetByIdAsync_AfterDelete_ReturnsNull()
    {
        // Arrange
        var created = await _expenseService.CreateAsync(_userId, new CreateExpenseRequest
        {
            CategoryId = _categoryId,
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow
        });
        await _expenseService.DeleteAsync(_userId, created.Id);

        // Act
        var result = await _expenseService.GetByIdAsync(_userId, created.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion
}
