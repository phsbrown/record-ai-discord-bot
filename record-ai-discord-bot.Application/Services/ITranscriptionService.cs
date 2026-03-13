using record_ai_discord_bot.Application.DTOs;

namespace record_ai_discord_bot.Application.Services;

public interface ITranscriptionService
{
    Task<AudioTranscriptionResult> TranscribeAsync(AudioTranscriptionRequest request, CancellationToken cancellationToken = default);
}
