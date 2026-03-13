namespace record_ai_discord_bot.Tests;

[TestClass]
public class WebTests
{
    [TestMethod]
    public async Task AppHostProgram_ShouldRegisterWhisperOllamaAndWorkerResources()
    {
        var appHostProgramPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "record-ai-discord-bot.AppHost",
            "Program.cs"));

        var programSource = await File.ReadAllTextAsync(appHostProgramPath);

        StringAssert.Contains(programSource, "AddContainer(\"whisper\"");
        StringAssert.Contains(programSource, "localai/localai");
        StringAssert.Contains(programSource, "AddContainer(\"ollama\"");
        StringAssert.Contains(programSource, "AddProject<Projects.record_ai_discord_bot_Worker>(\"worker\")");
        StringAssert.Contains(programSource, "AudioPersistence__RecordingsRootPath");
    }
}
