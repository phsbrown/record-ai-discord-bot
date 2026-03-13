namespace record_ai_discord_bot.Application.Services;

public interface IInboundVoiceFrameSource
{
    event EventHandler<UserPcmAudioFrameReceivedEventArgs>? FrameReceived;

    bool IsReceivingSupported { get; }

    string ReceiveSupportMessage { get; }

    Task StartAsync(AudioCaptureSession session, CancellationToken cancellationToken = default);

    Task StopAsync(string meetingId, CancellationToken cancellationToken = default);
}
