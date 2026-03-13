using record_ai_discord_bot.Domain.Entities;

namespace record_ai_discord_bot.Application.Services;

public sealed class UserPcmAudioFrameReceivedEventArgs : EventArgs
{
    public UserPcmAudioFrameReceivedEventArgs(UserPcmAudioFrame frame)
    {
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
    }

    public UserPcmAudioFrame Frame { get; }
}
