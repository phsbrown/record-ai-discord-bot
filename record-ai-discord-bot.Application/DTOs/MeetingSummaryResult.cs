namespace record_ai_discord_bot.Application.DTOs;

public sealed record MeetingSummaryResult(
    string MeetingId,
    string SummaryFilePath,
    string SummaryMarkdown,
    IReadOnlyDictionary<ulong, string> TranscriptFilesByUser);
