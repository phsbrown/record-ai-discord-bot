namespace record_ai_discord_bot.Application.Services;

public sealed record AudioCaptureSession(string MeetingId, ulong GuildId, ulong VoiceChannelId);
