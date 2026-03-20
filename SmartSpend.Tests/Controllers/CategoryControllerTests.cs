using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartSpend.API.Controllers;
using SmartSpend.Core.DTOs.Categories;
using SmartSpend.Core.Interfaces;

namespace SmartSpend.Tests.Controllers;

public class CategoryControllerTests
{
    private readonly Mock<ICategoryService> _categoryServiceMock;
    private readonly CategoryController _controller;
    private const int TestUserId = 1;

    public CategoryControllerTests()
    {
        _categoryServiceMock = new Mock<ICategoryService>();
        _controller = new CategoryController(_categoryServiceMock.Object);

        // Set up authenticated user context
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    #region Controller Attribute Tests

    [Fact]
    public void CategoryController_HasApiControllerAttribute()
    {
        typeof(CategoryController)
            .GetCustomAttributes(typeof(ApiControllerAttribute), true)
            .Should().NotBeEmpty();
    }

    [Fact]
    public void CategoryController_HasAuthorizeAttribute()
    {
        typeof(CategoryController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Should().NotBeEmpty();
    }

    [Fact]
    public void CategoryController_HasRouteAttribute()
    {
        var routeAttributes = typeof(CategoryController)
            .GetCustomAttributes(typeof(RouteAttribute), true)
            .Cast<RouteAttribute>()
            .ToList();

        routeAttributes.Should().NotBeEmpty();
        routeAttributes.Should().Contain(a => a.Template == "api/[controller]");
    }

    #endregion

    #region GetAll Tests

    [Fact]
    public async Task GetAll_ReturnsOkWithCategories()
    {
        // Arrange
        var categories = new List<CategoryResponse>
        {
            new() { Id = 1, Name = "Food", Icon = "🍔", IsDefault = true },
            new() { Id = 2, Name = "Transport", Icon = "🚗", IsDefault = true },
            new() { Id = 3, Name = "Custom", Icon = "⭐", IsDefault = false }
        };

        _categoryServiceMock
            .Setup(s => s.GetAllAsync(TestUserId))
            .ReturnsAsync(categories);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<IEnumerable<CategoryResponse>>().Subject;
        response.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAll_CallsServiceWithCorrectUserId()
    {
        // Arrange
        _categoryServiceMock
            .Setup(s => s.GetAllAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<CategoryResponse>());

        // Act
        await _controller.GetAll();

        // Assert
        _categoryServiceMock.Verify(s => s.GetAllAsync(TestUserId), Times.Once);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetById_ExistingCategory_ReturnsOk()
    {
        // Arrange
        var category = new CategoryResponse { Id = 1, Name = "Food", Icon = "🍔", IsDefault = true };

        _categoryServiceMock
            .Setup(s => s.GetByIdAsync(TestUserId, 1))
            .ReturnsAsync(category);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CategoryResponse>().Subject;
        response.Name.Should().Be("Food");
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        // Arrange
        _categoryServiceMock
            .Setup(s => s.GetByIdAsync(TestUserId, 999))
            .ReturnsAsync((CategoryResponse?)null);

        // Act
        var result = await _controller.GetById(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetById_CallsServiceWithCorrectParams()
    {
        // Arrange
        _categoryServiceMock
            .Setup(s => s.GetByIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((CategoryResponse?)null);

        // Act
        await _controller.GetById(5);

        // Assert
        _categoryServiceMock.Verify(s => s.GetByIdAsync(TestUserId, 5), Times.Once);
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreatedAtAction()
    {
        // Arrange
        var request = new CreateCategoryRequest { Name = "Entertainment", Icon = "🎮" };
        var created = new CategoryResponse { Id = 10, Name = "Entertainment", Icon = "🎮", IsDefault = false };

        _categoryServiceMock
            .Setup(s => s.CreateAsync(TestUserId, request))
            .ReturnsAsync(created);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(CategoryController.GetById));
        createdResult.RouteValues!["id"].Should().Be(10);
        var response = createdResult.Value.Should().BeOfType<CategoryResponse>().Subject;
        response.Name.Should().Be("Entertainment");
        response.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateCategoryRequest { Name = "Food" };

        _categoryServiceMock
            .Setup(s => s.CreateAsync(TestUserId, request))
            .ThrowsAsync(new InvalidOperationException("Category with this name already exists"));

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_CallsServiceWithCorrectParams()
    {
        // Arrange
        var request = new CreateCategoryRequest { Name = "Test", Icon = "🔵" };

        _categoryServiceMock
            .Setup(s => s.CreateAsync(It.IsAny<int>(), It.IsAny<CreateCategoryRequest>()))
            .ReturnsAsync(new CategoryResponse { Id = 1, Name = "Test" });

        // Act
        await _controller.Create(request);

        // Assert
        _categoryServiceMock.Verify(
            s => s.CreateAsync(TestUserId, It.Is<CreateCategoryRequest>(
                r => r.Name == "Test" && r.Icon == "🔵")),
            Times.Once);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_ExistingCategory_ReturnsOk()
    {
        // Arrange
        var request = new UpdateCategoryRequest { Name = "Updated", Icon = "🔴" };
        var updated = new CategoryResponse { Id = 5, Name = "Updated", Icon = "🔴", IsDefault = false };

        _categoryServiceMock
            .Setup(s => s.UpdateAsync(TestUserId, 5, request))
            .ReturnsAsync(updated);

        // Act
        var result = await _controller.Update(5, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CategoryResponse>().Subject;
        response.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task Update_NonExistentOrDefault_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateCategoryRequest { Name = "Hacked" };

        _categoryServiceMock
            .Setup(s => s.UpdateAsync(TestUserId, 999, request))
            .ReturnsAsync((CategoryResponse?)null);

        // Act
        var result = await _controller.Update(999, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Update_CallsServiceWithCorrectParams()
    {
        // Arrange
        var request = new UpdateCategoryRequest { Name = "New", Icon = "🟢" };

        _categoryServiceMock
            .Setup(s => s.UpdateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<UpdateCategoryRequest>()))
            .ReturnsAsync((CategoryResponse?)null);

        // Act
        await _controller.Update(7, request);

        // Assert
        _categoryServiceMock.Verify(
            s => s.UpdateAsync(TestUserId, 7, It.Is<UpdateCategoryRequest>(
                r => r.Name == "New" && r.Icon == "🟢")),
            Times.Once);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_ExistingUserCategory_ReturnsNoContent()
    {
        // Arrange
        _categoryServiceMock
            .Setup(s => s.DeleteAsync(TestUserId, 5))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(5);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_NonExistentOrDefault_ReturnsNotFound()
    {
        // Arrange
        _categoryServiceMock
            .Setup(s => s.DeleteAsync(TestUserId, 999))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Delete(999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Delete_CallsServiceWithCorrectParams()
    {
        // Arrange
        _categoryServiceMock
            .Setup(s => s.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(false);

        // Act
        await _controller.Delete(3);

        // Assert
        _categoryServiceMock.Verify(s => s.DeleteAsync(TestUserId, 3), Times.Once);
    }

    #endregion
}
