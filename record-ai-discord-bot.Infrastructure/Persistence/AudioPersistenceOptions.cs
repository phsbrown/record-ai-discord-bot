namespace record_ai_discord_bot.Infrastructure.Persistence;

public sealed class AudioPersistenceOptions
{
    public const string SectionName = "AudioPersistence";

    public string RecordingsRootPath { get; init; } = "recordings";
}
