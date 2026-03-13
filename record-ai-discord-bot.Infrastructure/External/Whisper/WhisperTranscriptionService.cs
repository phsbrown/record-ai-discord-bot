using record_ai_discord_bot.Application.DTOs;
using record_ai_discord_bot.Application.Services;

namespace record_ai_discord_bot.Infrastructure.External.Whisper;

public sealed class WhisperTranscriptionService(IWhisperClient whisperClient) : ITranscriptionService
{
    public async Task<AudioTranscriptionResult> TranscribeAsync(AudioTranscriptionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var clientRequest = new WhisperTranscriptionRequest(
            request.AudioStream,
            request.FileName,
            request.ContentType,
            request.Model,
            request.Language,
            request.Prompt);

        var response = await whisperClient.TranscribeAsync(clientRequest, cancellationToken);
        return new AudioTranscriptionResult(response.Text);
    }
}
