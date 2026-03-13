namespace record_ai_discord_bot.Infrastructure.External.Prompts;

public static class SummaryPromptBuilder
{
    public static string BuildSummaryPrompt(string transcript, string? additionalInstructions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);

        var instructions = string.IsNullOrWhiteSpace(additionalInstructions)
            ? string.Empty
            : $"Additional instructions: {additionalInstructions.Trim()}\n\n";

        return
            "You are summarizing a Discord voice conversation. " +
            "Return Markdown with the following headings in order: " +
            "## Summary, ## Key Decisions, ## Action Items, and ## Open Questions. " +
            "Keep the summary factual, concise, and based only on the transcript. " +
            "Use bullet lists for decisions, actions, and open questions.\n\n" +
            instructions +
            "Transcript:\n" +
            transcript;
    }
}
