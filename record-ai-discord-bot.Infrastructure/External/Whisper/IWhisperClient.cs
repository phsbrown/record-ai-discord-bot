namespace record_ai_discord_bot.Infrastructure.External.Whisper;

public interface IWhisperClient
{
    Task<WhisperTranscriptionResponse> TranscribeAsync(WhisperTranscriptionRequest request, CancellationToken cancellationToken = default);
}
