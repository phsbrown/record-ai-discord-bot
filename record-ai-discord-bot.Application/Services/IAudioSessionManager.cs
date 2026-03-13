using record_ai_discord_bot.Domain.Entities;

namespace record_ai_discord_bot.Application.Services;

public interface IAudioSessionManager
{
    Task StartMeetingSessionAsync(string meetingId, CancellationToken cancellationToken = default);

    ValueTask AppendFrameAsync(UserPcmAudioFrame frame, CancellationToken cancellationToken = default);

    Task StopMeetingSessionAsync(string meetingId, CancellationToken cancellationToken = default);
}
