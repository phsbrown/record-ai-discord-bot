using record_ai_discord_bot.Application.DTOs;

namespace record_ai_discord_bot.Application.Services;

public interface IMeetingArtifactStore
{
    Task<IReadOnlyList<RecordedAudioFile>> ListRecordedAudioFilesAsync(
        string meetingId,
        CancellationToken cancellationToken = default);

    Task<string> SaveTranscriptAsync(
        string meetingId,
        ulong userId,
        string username,
        string transcript,
        CancellationToken cancellationToken = default);

    Task<string> SaveSummaryAsync(
        string meetingId,
        string markdownContent,
        CancellationToken cancellationToken = default);
}
