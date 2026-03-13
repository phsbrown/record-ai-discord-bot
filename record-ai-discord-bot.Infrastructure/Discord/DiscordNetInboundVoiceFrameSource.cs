using System.Collections.Concurrent;
using Concentus.Structs;
using Discord.Audio;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using record_ai_discord_bot.Application.Services;
using record_ai_discord_bot.Domain.Entities;
using record_ai_discord_bot.Domain.ValueObjects;
using record_ai_discord_bot.Infrastructure.Persistence;

namespace record_ai_discord_bot.Infrastructure.Discord;

public sealed class DiscordNetInboundVoiceFrameSource(
    DiscordSocketClient discordClient,
    ILogger<DiscordNetInboundVoiceFrameSource> logger)
    : IDiscordAudioClientAwareVoiceFrameSource
{
    private static readonly PcmAudioFormat OutputFormat = new(48_000, 2, 16);
    private const int SamplesPerChannel = 960;
    private const int Channels = 2;

    private readonly ConcurrentDictionary<string, MeetingStreamSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<UserPcmAudioFrameReceivedEventArgs>? FrameReceived;

    public bool IsReceivingSupported => true;

    public string ReceiveSupportMessage =>
        "Discord.Net inbound voice receive is enabled through IAudioClient.StreamCreated and Opus decoding to PCM/WAV.";

    public void RegisterAudioClient(string meetingId, ulong guildId, IAudioClient audioClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meetingId);
        ArgumentNullException.ThrowIfNull(audioClient);
        var normalizedMeetingId = MeetingStoragePathHelper.NormalizeMeetingId(meetingId);

        var session = _sessions.GetOrAdd(
            normalizedMeetingId,
            _ => new MeetingStreamSession(normalizedMeetingId, guildId, audioClient));
        session.GuildId = guildId;
        session.AudioClient = audioClient;
    }

    public Task StartAsync(AudioCaptureSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var normalizedMeetingId = MeetingStoragePathHelper.NormalizeMeetingId(session.MeetingId);

        if (!_sessions.TryGetValue(normalizedMeetingId, out var meetingSession) || meetingSession.AudioClient is null)
        {
            throw new InvalidOperationException(
                $"No Discord audio client was registered for meeting '{normalizedMeetingId}'.");
        }

        if (meetingSession.Started)
        {
            return Task.CompletedTask;
        }

        meetingSession.Started = true;

        meetingSession.StreamCreatedHandler = async (userId, audioStream) =>
        {
            StartUserStreamPump(meetingSession, userId, audioStream);
            await Task.CompletedTask.ConfigureAwait(false);
        };

        meetingSession.StreamDestroyedHandler = async userId =>
        {
            StopUserStreamPump(meetingSession, userId);
            await Task.CompletedTask.ConfigureAwait(false);
        };

        meetingSession.AudioClient.StreamCreated += meetingSession.StreamCreatedHandler;
        meetingSession.AudioClient.StreamDestroyed += meetingSession.StreamDestroyedHandler;

        foreach (var activeStream in meetingSession.AudioClient.GetStreams())
        {
            StartUserStreamPump(meetingSession, activeStream.Key, activeStream.Value);
        }

        logger.LogInformation(
            "Started Discord inbound voice receive for meeting {MeetingId}.",
            normalizedMeetingId);

        return Task.CompletedTask;
    }

    public async Task StopAsync(string meetingId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(meetingId))
        {
            return;
        }

        var normalizedMeetingId = MeetingStoragePathHelper.NormalizeMeetingId(meetingId);

        if (!_sessions.TryRemove(normalizedMeetingId, out var session))
        {
            return;
        }

        if (session.AudioClient is not null)
        {
            if (session.StreamCreatedHandler is not null)
            {
                session.AudioClient.StreamCreated -= session.StreamCreatedHandler;
            }

            if (session.StreamDestroyedHandler is not null)
            {
                session.AudioClient.StreamDestroyed -= session.StreamDestroyedHandler;
            }
        }

        foreach (var streamSession in session.UserStreams.Values)
        {
            streamSession.CancellationSource.Cancel();
        }

        var pumpTasks = session.UserStreams.Values
            .Select(streamSession => streamSession.PumpTask)
            .Where(static task => task is not null)
            .Cast<Task>()
            .ToArray();

        session.UserStreams.Clear();

        if (pumpTasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(pumpTasks).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Cancelled while waiting for inbound audio pumps to stop for meeting {MeetingId}.",
                    normalizedMeetingId);
            }
        }
    }

    private void StartUserStreamPump(MeetingStreamSession meetingSession, ulong userId, AudioInStream audioStream)
    {
        meetingSession.UserStreams.AddOrUpdate(
            userId,
            _ => CreateUserStreamSession(meetingSession, userId, audioStream),
            (_, existing) =>
            {
                existing.CancellationSource.Cancel();
                return CreateUserStreamSession(meetingSession, userId, audioStream);
            });
    }

    private void StopUserStreamPump(MeetingStreamSession meetingSession, ulong userId)
    {
        if (meetingSession.UserStreams.TryRemove(userId, out var userStream))
        {
            userStream.CancellationSource.Cancel();
        }
    }

    private UserStreamSession CreateUserStreamSession(
        MeetingStreamSession meetingSession,
        ulong userId,
        AudioInStream audioStream)
    {
        var cancellationSource = new CancellationTokenSource();
        var decoder = OpusDecoder.Create(OutputFormat.SampleRateHz, OutputFormat.ChannelCount);
        var username = ResolveUsername(meetingSession.GuildId, userId);

        var session = new UserStreamSession(
            userId,
            username,
            audioStream,
            decoder,
            cancellationSource);

        session.PumpTask = Task.Run(
            () => PumpStreamAsync(meetingSession.MeetingId, session),
            cancellationSource.Token);

        logger.LogInformation(
            "Started inbound audio pump for meeting {MeetingId}, user {UserId} ({Username}).",
            meetingSession.MeetingId,
            userId,
            username);

        return session;
    }

    private async Task PumpStreamAsync(string meetingId, UserStreamSession userStream)
    {
        var pcmBuffer = new short[SamplesPerChannel * Channels];

        try
        {
            while (!userStream.CancellationSource.IsCancellationRequested)
            {
                RTPFrame frame;
                try
                {
                    frame = await userStream.AudioStream
                        .ReadFrameAsync(userStream.CancellationSource.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (frame.Payload is null || frame.Payload.Length == 0)
                {
                    continue;
                }

                var decodedSamplesPerChannel = userStream.Decoder.Decode(
                    frame.Payload,
                    0,
                    frame.Payload.Length,
                    pcmBuffer,
                    0,
                    SamplesPerChannel,
                    false);

                if (decodedSamplesPerChannel <= 0)
                {
                    continue;
                }

                var pcmByteCount = decodedSamplesPerChannel * Channels * sizeof(short);
                var pcmBytes = new byte[pcmByteCount];
                Buffer.BlockCopy(pcmBuffer, 0, pcmBytes, 0, pcmByteCount);

                FrameReceived?.Invoke(
                    this,
                    new UserPcmAudioFrameReceivedEventArgs(
                        new UserPcmAudioFrame(
                            meetingId,
                            userStream.UserId,
                            userStream.Username,
                            DateTimeOffset.UtcNow,
                            OutputFormat,
                            pcmBytes)));
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Inbound audio pump failed for meeting {MeetingId}, user {UserId}.",
                meetingId,
                userStream.UserId);
        }
    }

    private string ResolveUsername(ulong guildId, ulong userId)
    {
        var guild = discordClient.GetGuild(guildId);
        var user = guild?.GetUser(userId);
        return user?.DisplayName ?? user?.Username ?? userId.ToString();
    }

    private sealed class MeetingStreamSession(string meetingId, ulong guildId, IAudioClient? audioClient)
    {
        public string MeetingId { get; } = meetingId;

        public ulong GuildId { get; set; } = guildId;

        public IAudioClient? AudioClient { get; set; } = audioClient;

        public bool Started { get; set; }

        public Func<ulong, AudioInStream, Task>? StreamCreatedHandler { get; set; }

        public Func<ulong, Task>? StreamDestroyedHandler { get; set; }

        public ConcurrentDictionary<ulong, UserStreamSession> UserStreams { get; } = new();
    }

    private sealed class UserStreamSession(
        ulong userId,
        string username,
        AudioInStream audioStream,
        OpusDecoder decoder,
        CancellationTokenSource cancellationSource)
    {
        public ulong UserId { get; } = userId;

        public string Username { get; } = username;

        public AudioInStream AudioStream { get; } = audioStream;

        public OpusDecoder Decoder { get; } = decoder;

        public CancellationTokenSource CancellationSource { get; } = cancellationSource;

        public Task? PumpTask { get; set; }
    }
}
