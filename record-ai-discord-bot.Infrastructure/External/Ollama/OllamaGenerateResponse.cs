using System.Text.Json.Serialization;

namespace record_ai_discord_bot.Infrastructure.External.Ollama;

public sealed record OllamaGenerateResponse(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("response")] string Response,
    [property: JsonPropertyName("done")] bool Done,
    [property: JsonPropertyName("done_reason")] string? DoneReason,
    [property: JsonPropertyName("total_duration")] long? TotalDuration,
    [property: JsonPropertyName("load_duration")] long? LoadDuration,
    [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount,
    [property: JsonPropertyName("prompt_eval_duration")] long? PromptEvalDuration,
    [property: JsonPropertyName("eval_count")] int? EvalCount,
    [property: JsonPropertyName("eval_duration")] long? EvalDuration);
