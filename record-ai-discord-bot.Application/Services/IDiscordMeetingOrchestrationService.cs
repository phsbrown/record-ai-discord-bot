namespace record_ai_discord_bot.Application.Services;

public interface IDiscordMeetingOrchestrationService
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StartMeetingRecordingAsync(
        string meetingId,
        ulong guildId,
        ulong voiceChannelId,
        CancellationToken cancellationToken = default);

    Task StopMeetingRecordingAsync(string meetingId, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
