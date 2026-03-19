using SmartSpend.Core.DTOs.Webhooks;
using SmartSpend.Core.Models;

namespace SmartSpend.Core.Interfaces;

public interface IInsightService
{
    Task<AIInsight> CreateInsightAsync(CreateInsightRequest request);
}
