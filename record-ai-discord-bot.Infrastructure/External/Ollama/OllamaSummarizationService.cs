using record_ai_discord_bot.Application.DTOs;
using record_ai_discord_bot.Application.Services;
using record_ai_discord_bot.Infrastructure.External.Prompts;

namespace record_ai_discord_bot.Infrastructure.External.Ollama;

public sealed class OllamaSummarizationService(IOllamaClient ollamaClient) : ISummarizationService
{
    public async Task<SummarizationResult> SummarizeAsync(SummarizationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prompt = SummaryPromptBuilder.BuildSummaryPrompt(request.Transcript, request.AdditionalInstructions);
        var generation = await ollamaClient.GenerateAsync(
            new OllamaGenerateRequest(request.Model, prompt, Stream: false),
            cancellationToken);

        return new SummarizationResult(generation.Response.Trim(), generation.Model);
    }
}
