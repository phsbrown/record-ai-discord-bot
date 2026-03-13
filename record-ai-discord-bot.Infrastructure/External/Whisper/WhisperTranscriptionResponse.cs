using System.Text.Json.Serialization;

namespace record_ai_discord_bot.Infrastructure.External.Whisper;

public sealed record WhisperTranscriptionResponse([property: JsonPropertyName("text")] string Text);
