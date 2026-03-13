namespace record_ai_discord_bot.Worker;

public sealed class MeetingAutomationOptions
{
    public const string SectionName = "MeetingAutomation";

    public bool StartRecordingOnStartup { get; init; }

    public bool GenerateSummaryOnShutdown { get; init; }

    public string MeetingId { get; init; } = "local-discord-meeting";

    public ulong GuildId { get; init; }

    public ulong VoiceChannelId { get; init; }

    public string SummaryModel { get; init; } = "llama3.1:8b";

    public string WhisperModel { get; init; } = "whisper-1";

    public string? AdditionalSummaryInstructions { get; init; }
}
