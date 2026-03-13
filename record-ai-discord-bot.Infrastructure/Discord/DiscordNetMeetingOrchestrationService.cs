using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using record_ai_discord_bot.Application.Services;
using record_ai_discord_bot.Infrastructure.Persistence;

namespace record_ai_discord_bot.Infrastructure.Discord;

public sealed class DiscordNetMeetingOrchestrationService : IDiscordMeetingOrchestrationService, IAsyncDisposable
{
    private readonly DiscordSocketClient _discordClient;
    private readonly IInboundVoiceFrameSource _inboundVoiceFrameSource;
    private readonly IAudioSessionManager _audioSessionManager;
    private readonly DiscordBotOptions _options;
    private readonly ILogger<DiscordNetMeetingOrchestrationService> _logger;
    private readonly ConcurrentDictionary<string, ActiveRecordingSession> _activeSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    private bool _started;

    public DiscordNetMeetingOrchestrationService(
        DiscordSocketClient discordClient,
        IInboundVoiceFrameSource inboundVoiceFrameSource,
        IAudioSessionManager audioSessionManager,
        IOptions<DiscordBotOptions> options,
        ILogger<DiscordNetMeetingOrchestrationService> logger)
    {
        _discordClient = discordClient;
        _inboundVoiceFrameSource = inboundVoiceFrameSource;
        _audioSessionManager = audioSessionManager;
        _options = options?.Value ?? new DiscordBotOptions();
        _logger = logger;

        _discordClient.Log += OnDiscordLogAsync;
        _inboundVoiceFrameSource.FrameReceived += OnFrameReceived;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_started)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_options.Token))
            {
                throw new InvalidOperationException(
                    $"Discord bot token is missing. Configure {DiscordBotOptions.SectionName}:Token.");
            }

            await _discordClient.LoginAsync(TokenType.Bot, _options.Token).ConfigureAwait(false);
            await _discordClient.StartAsync().ConfigureAwait(false);

            _started = true;

            _logger.LogInformation(
                "Discord bot started. Inbound receive supported: {IsSupported}. Message: {SupportMessage}",
                _inboundVoiceFrameSource.IsReceivingSupported,
                _inboundVoiceFrameSource.ReceiveSupportMessage);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StartMeetingRecordingAsync(
        string meetingId,
        ulong guildId,
        ulong voiceChannelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meetingId);
        var normalizedMeetingId = MeetingStoragePathHelper.NormalizeMeetingId(meetingId);

        await StartAsync(cancellationToken).ConfigureAwait(false);
        await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_activeSessions.ContainsKey(normalizedMeetingId))
            {
                _logger.LogWarning("Meeting {MeetingId} is already active. Ignoring duplicate start request.", normalizedMeetingId);
                return;
            }

            var guild = _discordClient.GetGuild(guildId)
                ?? throw new InvalidOperationException($"Guild {guildId} was not found by this bot.");

            var voiceChannel = guild.GetVoiceChannel(voiceChannelId)
                ?? throw new InvalidOperationException($"Voice channel {voiceChannelId} was not found in guild {guildId}.");

            _logger.LogInformation(
                "Connecting bot to guild {GuildId}, voice channel {VoiceChannelId} for meeting {MeetingId}.",
                guildId,
                voiceChannelId,
                normalizedMeetingId);

            var audioClient = await voiceChannel.ConnectAsync().ConfigureAwait(false);
            if (_inboundVoiceFrameSource is IDiscordAudioClientAwareVoiceFrameSource audioClientAwareVoiceFrameSource)
            {
                audioClientAwareVoiceFrameSource.RegisterAudioClient(normalizedMeetingId, guildId, audioClient);
            }

            var activeSession = new ActiveRecordingSession(normalizedMeetingId, guildId, voiceChannelId, audioClient);

            try
            {
                await _audioSessionManager.StartMeetingSessionAsync(normalizedMeetingId, cancellationToken).ConfigureAwait(false);

                var captureSession = new AudioCaptureSession(normalizedMeetingId, guildId, voiceChannelId);
                await _inboundVoiceFrameSource.StartAsync(captureSession, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await SafeShutdownSessionAsync(activeSession, cancellationToken).ConfigureAwait(false);
                throw;
            }

            if (!_activeSessions.TryAdd(normalizedMeetingId, activeSession))
            {
                await SafeShutdownSessionAsync(activeSession, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException($"Meeting {normalizedMeetingId} was started concurrently.");
            }
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async Task StopMeetingRecordingAsync(string meetingId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(meetingId))
        {
            return;
        }

        var normalizedMeetingId = MeetingStoragePathHelper.NormalizeMeetingId(meetingId);

        await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_activeSessions.TryRemove(normalizedMeetingId, out var session))
            {
                return;
            }

            await SafeShutdownSessionAsync(session, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            ActiveRecordingSession[] sessions;
            try
            {
                sessions = _activeSessions.Values.ToArray();
                _activeSessions.Clear();
            }
            finally
            {
                _sessionLock.Release();
            }

            foreach (var session in sessions)
            {
                await SafeShutdownSessionAsync(session, cancellationToken).ConfigureAwait(false);
            }

            if (_started)
            {
                await _discordClient.StopAsync().ConfigureAwait(false);
                await _discordClient.LogoutAsync().ConfigureAwait(false);
            }

            _started = false;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _inboundVoiceFrameSource.FrameReceived -= OnFrameReceived;
        _discordClient.Log -= OnDiscordLogAsync;

        await StopAsync().ConfigureAwait(false);

        _lifecycleLock.Dispose();
        _sessionLock.Dispose();
        _discordClient.Dispose();
    }

    private async Task SafeShutdownSessionAsync(ActiveRecordingSession session, CancellationToken cancellationToken)
    {
        try
        {
            await _inboundVoiceFrameSource.StopAsync(session.MeetingId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Error stopping inbound voice source for meeting {MeetingId}.", session.MeetingId);
        }

        try
        {
            await _audioSessionManager.StopMeetingSessionAsync(session.MeetingId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Error stopping audio persistence for meeting {MeetingId}.", session.MeetingId);
        }

        try
        {
            await session.AudioClient.StopAsync().ConfigureAwait(false);
            session.AudioClient.Dispose();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Error disconnecting audio client for meeting {MeetingId}.", session.MeetingId);
        }
    }

    private void OnFrameReceived(object? sender, UserPcmAudioFrameReceivedEventArgs eventArgs)
    {
        _ = PersistFrameAsync(eventArgs.Frame);
    }

    private async Task PersistFrameAsync(record_ai_discord_bot.Domain.Entities.UserPcmAudioFrame frame)
    {
        try
        {
            await _audioSessionManager.AppendFrameAsync(frame).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to persist frame for meeting {MeetingId}, user {UserId}.",
                frame.MeetingId,
                frame.UserId);
        }
    }

    private Task OnDiscordLogAsync(LogMessage message)
    {
        _logger.LogInformation(
            "Discord.Net: {Severity} - {Source}: {Message}",
            message.Severity,
            message.Source,
            message.Message);

        return Task.CompletedTask;
    }

    private sealed record ActiveRecordingSession(
        string MeetingId,
        ulong GuildId,
        ulong VoiceChannelId,
        IAudioClient AudioClient);
}
