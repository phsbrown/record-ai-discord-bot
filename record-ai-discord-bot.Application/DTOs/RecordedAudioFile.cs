namespace record_ai_discord_bot.Application.DTOs;

public sealed record RecordedAudioFile(
    string MeetingId,
    ulong UserId,
    string Username,
    string FilePath);
