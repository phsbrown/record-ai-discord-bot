namespace record_ai_discord_bot.Domain.Entities;

public sealed class MeetingRecording
{
    public MeetingRecording(
        string meetingId,
        ulong guildId,
        ulong voiceChannelId,
        string recordingsRootPath,
        IReadOnlyCollection<MeetingParticipant> participants)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meetingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordingsRootPath);

        MeetingId = meetingId;
        GuildId = guildId;
        VoiceChannelId = voiceChannelId;
        RecordingsRootPath = recordingsRootPath;
        Participants = participants ?? [];
        StartedAtUtc = DateTimeOffset.UtcNow;
    }

    public string MeetingId { get; }

    public ulong GuildId { get; }

    public ulong VoiceChannelId { get; }

    public string RecordingsRootPath { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public IReadOnlyCollection<MeetingParticipant> Participants { get; }
}
