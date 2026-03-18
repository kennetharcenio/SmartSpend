using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSpend.Core.DTOs.Categories;
using SmartSpend.Core.Interfaces;

namespace SmartSpend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoryController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetAll()
    {
        var userId = GetUserId();
        var categories = await _categoryService.GetAllAsync(userId);
        return Ok(categories);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryResponse>> GetById(int id)
    {
        var userId = GetUserId();
        var category = await _categoryService.GetByIdAsync(userId, id);

        if (category is null)
            return NotFound(new { message = "Category not found" });

        return Ok(category);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create([FromBody] CreateCategoryRequest request)
    {
        try
        {
            var userId = GetUserId();
            var category = await _categoryService.CreateAsync(userId, request);
            return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CategoryResponse>> Update(int id, [FromBody] UpdateCategoryRequest request)
    {
        var userId = GetUserId();
        var category = await _categoryService.UpdateAsync(userId, id, request);

        if (category is null)
            return NotFound(new { message = "Category not found or cannot be modified" });

        return Ok(category);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var userId = GetUserId();
        var deleted = await _categoryService.DeleteAsync(userId, id);

        if (!deleted)
            return NotFound(new { message = "Category not found or cannot be deleted" });

        return NoContent();
    }

    private int GetUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
