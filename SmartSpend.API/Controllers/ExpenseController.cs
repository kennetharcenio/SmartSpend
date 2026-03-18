using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSpend.Core.DTOs.Expenses;
using SmartSpend.Core.Interfaces;

namespace SmartSpend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpenseController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public ExpenseController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExpenseResponse>>> GetAll()
    {
        var userId = GetUserId();
        var expenses = await _expenseService.GetAllAsync(userId);
        return Ok(expenses);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ExpenseResponse>> GetById(int id)
    {
        var userId = GetUserId();
        var expense = await _expenseService.GetByIdAsync(userId, id);

        if (expense is null)
            return NotFound(new { message = "Expense not found" });

        return Ok(expense);
    }

    [HttpPost]
    public async Task<ActionResult<ExpenseResponse>> Create([FromBody] CreateExpenseRequest request)
    {
        try
        {
            var userId = GetUserId();
            var expense = await _expenseService.CreateAsync(userId, request);
            return CreatedAtAction(nameof(GetById), new { id = expense.Id }, expense);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ExpenseResponse>> Update(int id, [FromBody] UpdateExpenseRequest request)
    {
        try
        {
            var userId = GetUserId();
            var expense = await _expenseService.UpdateAsync(userId, id, request);

            if (expense is null)
                return NotFound(new { message = "Expense not found" });

            return Ok(expense);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var userId = GetUserId();
        var deleted = await _expenseService.DeleteAsync(userId, id);

        if (!deleted)
            return NotFound(new { message = "Expense not found" });

        return NoContent();
    }

    private int GetUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
