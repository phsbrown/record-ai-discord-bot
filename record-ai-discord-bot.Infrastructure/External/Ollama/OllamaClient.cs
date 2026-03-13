using System.Net.Http.Json;
using System.Text.Json;

namespace record_ai_discord_bot.Infrastructure.External.Ollama;

public sealed class OllamaClient(HttpClient httpClient) : IOllamaClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<OllamaGenerateResponse> GenerateAsync(OllamaGenerateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await httpClient.PostAsJsonAsync("/api/generate", request, SerializerOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Ollama generation request failed with status code {(int)response.StatusCode}. Body: {errorBody}");
        }

        var generation = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(SerializerOptions, cancellationToken);
        if (generation is null || string.IsNullOrWhiteSpace(generation.Response))
        {
            throw new InvalidOperationException("Ollama response did not contain generated text.");
        }

        return generation;
    }
}
