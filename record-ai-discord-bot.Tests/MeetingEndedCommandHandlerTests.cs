using Microsoft.Extensions.Logging.Abstractions;
using record_ai_discord_bot.Application.Commands;
using record_ai_discord_bot.Application.DTOs;
using record_ai_discord_bot.Application.Services;

namespace record_ai_discord_bot.Tests;

[TestClass]
public class MeetingEndedCommandHandlerTests
{
    [TestMethod]
    public async Task Handle_ShouldStopRecordingTranscribeAudioAndPersistSummary()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "discord-recorder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var audioFilePath = Path.Combine(tempDirectory, "audio.wav");
            await File.WriteAllBytesAsync(audioFilePath, [0x52, 0x49, 0x46, 0x46]);

            var orchestrationService = new FakeDiscordMeetingOrchestrationService();
            var artifactStore = new FakeMeetingArtifactStore(tempDirectory, audioFilePath);
            var transcriptionService = new FakeTranscriptionService();
            var summarizationService = new FakeSummarizationService();

            var handler = new MeetingEndedCommandHandler(
                orchestrationService,
                artifactStore,
                transcriptionService,
                summarizationService,
                NullLogger<MeetingEndedCommandHandler>.Instance);

            var result = await handler.Handle(
                new MeetingEndedCommand("meeting-123", AdditionalSummaryInstructions: "Return Markdown."),
                CancellationToken.None);

            Assert.IsTrue(orchestrationService.StopCalled);
            Assert.AreEqual("meeting-123", result.MeetingId);
            Assert.IsTrue(File.Exists(result.SummaryFilePath));
            Assert.AreEqual(1, result.TranscriptFilesByUser.Count);
            StringAssert.Contains(result.SummaryMarkdown, "## Summary");
            StringAssert.Contains(result.SummaryMarkdown, "Alice");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private sealed class FakeDiscordMeetingOrchestrationService : IDiscordMeetingOrchestrationService
    {
        public bool StopCalled { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StartMeetingRecordingAsync(
            string meetingId,
            ulong guildId,
            ulong voiceChannelId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopMeetingRecordingAsync(string meetingId, CancellationToken cancellationToken = default)
        {
            StopCalled = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMeetingArtifactStore(string rootPath, string audioFilePath) : IMeetingArtifactStore
    {
        public Task<IReadOnlyList<RecordedAudioFile>> ListRecordedAudioFilesAsync(
            string meetingId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RecordedAudioFile> files =
            [
                new RecordedAudioFile(meetingId, 42UL, "Alice", audioFilePath),
            ];

            return Task.FromResult(files);
        }

        public async Task<string> SaveTranscriptAsync(
            string meetingId,
            ulong userId,
            string username,
            string transcript,
            CancellationToken cancellationToken = default)
        {
            var path = Path.Combine(rootPath, $"{meetingId}-{userId}.txt");
            await File.WriteAllTextAsync(path, transcript, cancellationToken);
            return path;
        }

        public async Task<string> SaveSummaryAsync(
            string meetingId,
            string markdownContent,
            CancellationToken cancellationToken = default)
        {
            var path = Path.Combine(rootPath, $"{meetingId}-summary.md");
            await File.WriteAllTextAsync(path, markdownContent, cancellationToken);
            return path;
        }
    }

    private sealed class FakeTranscriptionService : ITranscriptionService
    {
        public Task<AudioTranscriptionResult> TranscribeAsync(
            AudioTranscriptionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AudioTranscriptionResult("Alice discussed the roadmap and assigned two action items."));
        }
    }

    private sealed class FakeSummarizationService : ISummarizationService
    {
        public Task<SummarizationResult> SummarizeAsync(
            SummarizationRequest request,
            CancellationToken cancellationToken = default)
        {
            var markdown = """
                ## Summary
                The team reviewed the roadmap.

                ## Key Decisions
                - Keep processing local.

                ## Action Items
                - Alice will prepare the next draft.

                ## Open Questions
                - Which model should be the default in Ollama?
                """;

            return Task.FromResult(new SummarizationResult(markdown, "test-model"));
        }
    }
}
