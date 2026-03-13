namespace record_ai_discord_bot.Infrastructure.External.Whisper;

public sealed record WhisperTranscriptionRequest(
    Stream AudioStream,
    string FileName,
    string ContentType,
    string Model,
    string? Language,
    string? Prompt);
