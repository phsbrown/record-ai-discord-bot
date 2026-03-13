using System.Text.Json.Serialization;

namespace record_ai_discord_bot.Infrastructure.External.Ollama;

public sealed record OllamaGenerateRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("stream")] bool Stream = false);
