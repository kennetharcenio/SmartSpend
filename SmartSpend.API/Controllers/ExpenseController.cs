using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSpend.Core.DTOs.Expense;
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

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<ActionResult<ExpenseResponse>> Create([FromBody] CreateExpenseRequest request)
    {
        try
        {
            var response = await _expenseService.CreateAsync(GetUserId(), request);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ExpenseResponse>> GetById(int id)
    {
        var response = await _expenseService.GetByIdAsync(GetUserId(), id);

        if (response is null)
        {
            return NotFound(new { message = "Expense not found" });
        }

        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExpenseResponse>>> GetAll()
    {
        var response = await _expenseService.GetAllAsync(GetUserId());
        return Ok(response);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ExpenseResponse>> Update(int id, [FromBody] UpdateExpenseRequest request)
    {
        try
        {
            var response = await _expenseService.UpdateAsync(GetUserId(), id, request);

            if (response is null)
            {
                return NotFound(new { message = "Expense not found" });
            }

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _expenseService.DeleteAsync(GetUserId(), id);

        if (!result)
        {
            return NotFound(new { message = "Expense not found" });
        }

        return NoContent();
    }
}
