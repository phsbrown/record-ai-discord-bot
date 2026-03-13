using System.Text.RegularExpressions;

namespace record_ai_discord_bot.Infrastructure.Persistence;

internal static partial class MeetingStoragePathHelper
{
    public static string NormalizeMeetingId(string meetingId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meetingId);

        var trimmed = meetingId.Trim();
        if (!MeetingIdPattern().IsMatch(trimmed))
        {
            throw new InvalidOperationException(
                $"Meeting id '{meetingId}' contains unsupported characters. Use only letters, numbers, underscores, and hyphens.");
        }

        return trimmed;
    }

    public static string CombineUnderRoot(string rootPath, string meetingId, params string[] extraSegments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var fullRootPath = Path.GetFullPath(rootPath);
        var safeMeetingId = NormalizeMeetingId(meetingId);

        var segments = new string[extraSegments.Length + 2];
        segments[0] = fullRootPath;
        segments[1] = safeMeetingId;
        Array.Copy(extraSegments, 0, segments, 2, extraSegments.Length);

        var combinedPath = Path.GetFullPath(Path.Combine(segments));
        var rootedPrefix = fullRootPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullRootPath
            : fullRootPath + Path.DirectorySeparatorChar;

        if (!combinedPath.StartsWith(rootedPrefix, StringComparison.Ordinal)
            && !string.Equals(combinedPath, fullRootPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resolved path '{combinedPath}' escapes configured recordings root '{fullRootPath}'.");
        }

        return combinedPath;
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex MeetingIdPattern();
}
