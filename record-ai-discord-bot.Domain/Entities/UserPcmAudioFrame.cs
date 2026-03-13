using record_ai_discord_bot.Domain.ValueObjects;

namespace record_ai_discord_bot.Domain.Entities;

public sealed class UserPcmAudioFrame
{
    public UserPcmAudioFrame(
        string meetingId,
        ulong userId,
        string username,
        DateTimeOffset capturedAtUtc,
        PcmAudioFormat format,
        ReadOnlyMemory<byte> pcmData)
    {
        if (string.IsNullOrWhiteSpace(meetingId))
        {
            throw new ArgumentException("Meeting id is required.", nameof(meetingId));
        }

        if (userId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), userId, "User id must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }

        if (pcmData.IsEmpty)
        {
            throw new ArgumentException("PCM payload cannot be empty.", nameof(pcmData));
        }

        MeetingId = meetingId;
        UserId = userId;
        Username = username;
        CapturedAtUtc = capturedAtUtc;
        Format = format ?? throw new ArgumentNullException(nameof(format));
        PcmData = pcmData;
    }

    public string MeetingId { get; }

    public ulong UserId { get; }

    public string Username { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public PcmAudioFormat Format { get; }

    public ReadOnlyMemory<byte> PcmData { get; }
}
