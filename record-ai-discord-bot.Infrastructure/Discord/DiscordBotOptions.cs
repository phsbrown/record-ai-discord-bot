namespace record_ai_discord_bot.Infrastructure.Discord;

public sealed class DiscordBotOptions
{
    public const string SectionName = "DiscordBot";

    public string Token { get; init; } = string.Empty;
}
