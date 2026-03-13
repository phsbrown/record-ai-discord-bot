using record_ai_discord_bot.Application.DTOs;
using record_ai_discord_bot.Application.Services;
using Microsoft.Extensions.Options;

namespace record_ai_discord_bot.Infrastructure.Persistence;

public sealed class FileSystemMeetingArtifactStore(IOptions<AudioPersistenceOptions> options) : IMeetingArtifactStore
{
    private readonly string _recordingsRootPath = string.IsNullOrWhiteSpace(options.Value.RecordingsRootPath)
        ? "recordings"
        : options.Value.RecordingsRootPath;

    public Task<IReadOnlyList<RecordedAudioFile>> ListRecordedAudioFilesAsync(
        string meetingId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var safeMeetingId = MeetingStoragePathHelper.NormalizeMeetingId(meetingId);

        var audioDirectory = MeetingStoragePathHelper.CombineUnderRoot(_recordingsRootPath, safeMeetingId, "audio");
        if (!Directory.Exists(audioDirectory))
        {
            return Task.FromResult<IReadOnlyList<RecordedAudioFile>>([]);
        }

        var files = Directory
            .EnumerateFiles(audioDirectory, "*.wav", SearchOption.TopDirectoryOnly)
            .Select(filePath => ToRecordedAudioFile(safeMeetingId, filePath))
            .OrderBy(static file => file.Username, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<RecordedAudioFile>>(files);
    }

    public async Task<string> SaveTranscriptAsync(
        string meetingId,
        ulong userId,
        string username,
        string transcript,
        CancellationToken cancellationToken = default)
    {
        var safeMeetingId = MeetingStoragePathHelper.NormalizeMeetingId(meetingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);

        var transcriptDirectory = MeetingStoragePathHelper.CombineUnderRoot(_recordingsRootPath, safeMeetingId, "transcripts");
        Directory.CreateDirectory(transcriptDirectory);

        var fileName = $"{userId}_{FileNameSanitizer.SanitizeUsername(username)}.txt";
        var filePath = Path.Combine(transcriptDirectory, fileName);
        await File.WriteAllTextAsync(filePath, transcript.Trim() + Environment.NewLine, cancellationToken).ConfigureAwait(false);

        return filePath;
    }

    public async Task<string> SaveSummaryAsync(
        string meetingId,
        string markdownContent,
        CancellationToken cancellationToken = default)
    {
        var safeMeetingId = MeetingStoragePathHelper.NormalizeMeetingId(meetingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(markdownContent);

        var meetingDirectory = MeetingStoragePathHelper.CombineUnderRoot(_recordingsRootPath, safeMeetingId);
        Directory.CreateDirectory(meetingDirectory);

        var filePath = Path.Combine(meetingDirectory, "summary.md");
        await File.WriteAllTextAsync(filePath, markdownContent.TrimEnd() + Environment.NewLine, cancellationToken).ConfigureAwait(false);

        return filePath;
    }

    private static RecordedAudioFile ToRecordedAudioFile(string meetingId, string filePath)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var separatorIndex = fileNameWithoutExtension.IndexOf('_');
        if (separatorIndex <= 0)
        {
            throw new InvalidOperationException(
                $"Recorded audio file '{filePath}' does not follow the expected '<userId>_<username>.wav' convention.");
        }

        var userIdSegment = fileNameWithoutExtension[..separatorIndex];
        if (!ulong.TryParse(userIdSegment, out var userId))
        {
            throw new InvalidOperationException(
                $"Recorded audio file '{filePath}' does not start with a valid Discord user id.");
        }

        var username = fileNameWithoutExtension[(separatorIndex + 1)..];
        return new RecordedAudioFile(meetingId, userId, username, filePath);
    }
}
