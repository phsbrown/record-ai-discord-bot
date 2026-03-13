using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using record_ai_discord_bot.Application.Services;
using record_ai_discord_bot.Infrastructure.Persistence;

namespace record_ai_discord_bot.Infrastructure.Discord;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiscordRecordingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<DiscordBotOptions>(configuration.GetSection(DiscordBotOptions.SectionName));
        services.Configure<AudioPersistenceOptions>(configuration.GetSection(AudioPersistenceOptions.SectionName));

        services.AddSingleton(_ =>
        {
            var clientConfig = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
                LogGatewayIntentWarnings = false,
            };

            return new DiscordSocketClient(clientConfig);
        });

        services.AddSingleton<IInboundVoiceFrameSource, DiscordNetInboundVoiceFrameSource>();
        services.AddSingleton<IAudioSessionManager, FileSystemAudioSessionManager>();
        services.AddSingleton<IMeetingArtifactStore, FileSystemMeetingArtifactStore>();
        services.AddSingleton<IDiscordMeetingOrchestrationService, DiscordNetMeetingOrchestrationService>();

        return services;
    }
}
