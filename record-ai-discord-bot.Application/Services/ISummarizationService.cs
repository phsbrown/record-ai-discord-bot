using record_ai_discord_bot.Application.DTOs;

namespace record_ai_discord_bot.Application.Services;

public interface ISummarizationService
{
    Task<SummarizationResult> SummarizeAsync(SummarizationRequest request, CancellationToken cancellationToken = default);
}
