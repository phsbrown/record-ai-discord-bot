namespace record_ai_discord_bot.Application.DTOs;

public sealed record SummarizationRequest(
    string Transcript,
    string Model = "llama3.1:8b",
    string? AdditionalInstructions = null);
