using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace record_ai_discord_bot.Infrastructure.External.Whisper;

public sealed class WhisperClient(HttpClient httpClient) : IWhisperClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<WhisperTranscriptionResponse> TranscribeAsync(WhisperTranscriptionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var formData = new MultipartFormDataContent();

        var streamContent = new StreamContent(request.AudioStream);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);

        formData.Add(streamContent, "file", request.FileName);
        formData.Add(new StringContent(request.Model), "model");

        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            formData.Add(new StringContent(request.Language), "language");
        }

        if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            formData.Add(new StringContent(request.Prompt), "prompt");
        }

        using var response = await httpClient.PostAsync("/v1/audio/transcriptions", formData, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Whisper transcription request failed with status code {(int)response.StatusCode}. Body: {errorBody}");
        }

        var transcription = await response.Content.ReadFromJsonAsync<WhisperTranscriptionResponse>(SerializerOptions, cancellationToken);
        if (transcription is null || string.IsNullOrWhiteSpace(transcription.Text))
        {
            throw new InvalidOperationException("Whisper response did not contain a transcription text.");
        }

        return transcription;
    }
}
