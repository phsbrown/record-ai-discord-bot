namespace record_ai_discord_bot.Application.DTOs;

public sealed record AudioTranscriptionRequest(
    Stream AudioStream,
    string FileName,
    string ContentType = "application/octet-stream",
    string Model = "whisper-1",
    string? Language = null,
    string? Prompt = null);
