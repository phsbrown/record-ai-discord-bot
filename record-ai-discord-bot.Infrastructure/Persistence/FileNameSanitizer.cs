namespace record_ai_discord_bot.Infrastructure.Persistence;

internal static class FileNameSanitizer
{
    public static string SanitizeUsername(string username)
    {
        var invalidFileNameChars = Path.GetInvalidFileNameChars();
        var safeChars = username
            .Trim()
            .Select(character => invalidFileNameChars.Contains(character) ? '_' : character)
            .ToArray();

        var safeValue = new string(safeChars);
        if (string.IsNullOrWhiteSpace(safeValue))
        {
            return "unknown-user";
        }

        return safeValue.Replace(' ', '_');
    }
}
