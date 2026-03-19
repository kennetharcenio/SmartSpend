using SmartSpend.Core.DTOs.Webhooks;

namespace SmartSpend.Core.Interfaces;

public interface IExpenseParsingService
{
    Task<ParseExpenseResponse> ParseExpenseAsync(ParseExpenseRequest request);
}
