using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSpend.Core.DTOs.Webhooks;
using SmartSpend.Core.Interfaces;

namespace SmartSpend.API.Controllers;

[ApiController]
[Route("api/webhooks")]
[Authorize(AuthenticationSchemes = "ApiKey")]
public class WebhooksController : ControllerBase
{
    private readonly IExpenseParsingService _parsingService;
    private readonly IExpenseSummaryService _summaryService;
    private readonly IInsightService _insightService;

    public WebhooksController(
        IExpenseParsingService parsingService,
        IExpenseSummaryService summaryService,
        IInsightService insightService)
    {
        _parsingService = parsingService;
        _summaryService = summaryService;
        _insightService = insightService;
    }

    [HttpPost("expenses/parse")]
    public async Task<ActionResult<ParseExpenseResponse>> ParseExpense([FromBody] ParseExpenseRequest request)
    {
        try
        {
            var result = await _parsingService.ParseExpenseAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("expenses/summary")]
    public async Task<ActionResult<ExpenseSummaryResponse>> GetExpenseSummary(
        [FromQuery] int userId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var result = await _summaryService.GetSummaryAsync(userId, from, to);
        return Ok(result);
    }

    [HttpPost("insights")]
    public async Task<ActionResult> CreateInsight([FromBody] CreateInsightRequest request)
    {
        try
        {
            var result = await _insightService.CreateInsightAsync(request);
            return CreatedAtAction(null, new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
