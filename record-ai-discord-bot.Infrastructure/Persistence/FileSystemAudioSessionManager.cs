using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using record_ai_discord_bot.Application.Services;
using record_ai_discord_bot.Domain.Entities;
using record_ai_discord_bot.Domain.ValueObjects;

namespace record_ai_discord_bot.Infrastructure.Persistence;

public sealed class FileSystemAudioSessionManager : IAudioSessionManager, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, MeetingAudioSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<FileSystemAudioSessionManager> _logger;
    private readonly string _recordingsRootPath;

    public FileSystemAudioSessionManager(
        IOptions<AudioPersistenceOptions> options,
        ILogger<FileSystemAudioSessionManager> logger)
    {
        _logger = logger;

        var configuredRootPath = options?.Value?.RecordingsRootPath;
        _recordingsRootPath = string.IsNullOrWhiteSpace(configuredRootPath)
            ? "recordings"
            : configuredRootPath;
    }

    public Task StartMeetingSessionAsync(string meetingId, CancellationToken cancellationToken = default)
    {
        var safeMeetingId = MeetingStoragePathHelper.NormalizeMeetingId(meetingId);

        var session = _sessions.GetOrAdd(safeMeetingId, id => new MeetingAudioSession(id, _logger));
        var audioDirectoryPath = GetAudioDirectoryPath(safeMeetingId);

        session.Initialize(audioDirectoryPath);

        _logger.LogInformation(
            "Started audio persistence session for meeting {MeetingId}. Audio path: {AudioDirectoryPath}",
            safeMeetingId,
            audioDirectoryPath);

        return Task.CompletedTask;
    }

    public async ValueTask AppendFrameAsync(UserPcmAudioFrame frame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var safeMeetingId = MeetingStoragePathHelper.NormalizeMeetingId(frame.MeetingId);

        if (!_sessions.TryGetValue(safeMeetingId, out var session))
        {
            throw new InvalidOperationException($"Meeting session '{safeMeetingId}' is not active.");
        }

        await session.AppendFrameAsync(frame, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopMeetingSessionAsync(string meetingId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(meetingId))
        {
            return;
        }

        var safeMeetingId = MeetingStoragePathHelper.NormalizeMeetingId(meetingId);

        if (!_sessions.TryRemove(safeMeetingId, out var session))
        {
            return;
        }

        await session.DisposeAsync().ConfigureAwait(false);

        _logger.LogInformation("Stopped audio persistence session for meeting {MeetingId}.", safeMeetingId);
    }

    public async ValueTask DisposeAsync()
    {
        var sessions = _sessions.Values.ToArray();
        _sessions.Clear();

        foreach (var session in sessions)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private string GetAudioDirectoryPath(string meetingId)
    {
        return MeetingStoragePathHelper.CombineUnderRoot(_recordingsRootPath, meetingId, "audio");
    }

    private sealed class MeetingAudioSession : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<ulong, WavFileWriter> _writers = new();
        private readonly Channel<UserPcmAudioFrame> _frameChannel = Channel.CreateBounded<UserPcmAudioFrame>(
            new BoundedChannelOptions(2048)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            });
        private readonly string _meetingId;
        private readonly ILogger _logger;
        private readonly Task _consumerTask;
        private string? _audioDirectoryPath;

        public MeetingAudioSession(string meetingId, ILogger logger)
        {
            _meetingId = meetingId;
            _logger = logger;
            _consumerTask = Task.Run(ProcessFramesAsync);
        }

        public void Initialize(string audioDirectoryPath)
        {
            _audioDirectoryPath ??= audioDirectoryPath;
            Directory.CreateDirectory(_audioDirectoryPath);
        }

        public ValueTask AppendFrameAsync(
            UserPcmAudioFrame frame,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_audioDirectoryPath is null)
            {
                throw new InvalidOperationException($"Meeting session '{_meetingId}' was not initialized.");
            }

            if (!_frameChannel.Writer.TryWrite(frame))
            {
                _logger.LogWarning(
                    "Dropped audio frame for meeting {MeetingId}, user {UserId} because the bounded persistence queue is full or closed.",
                    frame.MeetingId,
                    frame.UserId);
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            _frameChannel.Writer.TryComplete();
            await _consumerTask.ConfigureAwait(false);

            var writers = _writers.Values.ToArray();
            _writers.Clear();

            foreach (var writer in writers)
            {
                await writer.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async Task ProcessFramesAsync()
        {
            await foreach (var frame in _frameChannel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    if (_audioDirectoryPath is null)
                    {
                        continue;
                    }

                    var writer = _writers.GetOrAdd(frame.UserId, _ => CreateWriter(frame, _audioDirectoryPath, _logger));

                    if (!AreFormatsEqual(writer.Format, frame.Format))
                    {
                        _logger.LogWarning(
                            "Skipped frame for meeting {MeetingId}, user {UserId}. Frame format {FrameFormat} does not match established format {EstablishedFormat}.",
                            frame.MeetingId,
                            frame.UserId,
                            DescribeFormat(frame.Format),
                            DescribeFormat(writer.Format));

                        continue;
                    }

                    await writer.WritePcmAsync(frame.PcmData, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Failed to persist audio frame for meeting {MeetingId}, user {UserId}.",
                        frame.MeetingId,
                        frame.UserId);
                }
            }
        }

        private static WavFileWriter CreateWriter(UserPcmAudioFrame frame, string audioDirectoryPath, ILogger logger)
        {
            var safeUsername = FileNameSanitizer.SanitizeUsername(frame.Username);
            var fileName = $"{frame.UserId}_{safeUsername}.wav";
            var filePath = Path.Combine(audioDirectoryPath, fileName);

            logger.LogInformation(
                "Creating WAV writer for meeting {MeetingId}, user {UserId}, username {Username}. Path: {FilePath}",
                frame.MeetingId,
                frame.UserId,
                frame.Username,
                filePath);

            return new WavFileWriter(filePath, frame.Format);
        }

        private static bool AreFormatsEqual(PcmAudioFormat left, PcmAudioFormat right)
        {
            return left.SampleRateHz == right.SampleRateHz
                && left.ChannelCount == right.ChannelCount
                && left.BitsPerSample == right.BitsPerSample;
        }

        private static string DescribeFormat(PcmAudioFormat format)
        {
            return $"{format.SampleRateHz}Hz/{format.ChannelCount}ch/{format.BitsPerSample}bit";
        }
    }
}
