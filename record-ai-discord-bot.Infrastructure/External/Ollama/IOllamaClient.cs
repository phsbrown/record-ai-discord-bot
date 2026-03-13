namespace record_ai_discord_bot.Infrastructure.External.Ollama;

public interface IOllamaClient
{
    Task<OllamaGenerateResponse> GenerateAsync(OllamaGenerateRequest request, CancellationToken cancellationToken = default);
}
