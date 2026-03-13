using Discord.Audio;
using record_ai_discord_bot.Application.Services;

namespace record_ai_discord_bot.Infrastructure.Discord;

internal interface IDiscordAudioClientAwareVoiceFrameSource : IInboundVoiceFrameSource
{
    void RegisterAudioClient(string meetingId, ulong guildId, IAudioClient audioClient);
}
