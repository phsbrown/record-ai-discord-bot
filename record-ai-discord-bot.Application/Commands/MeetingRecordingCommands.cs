using System.Collections.Concurrent;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using record_ai_discord_bot.Application.DTOs;
using record_ai_discord_bot.Application.Services;

namespace record_ai_discord_bot.Application.Commands;

public sealed record StartMeetingRecordingCommand(
    string MeetingId,
    ulong GuildId,
    ulong VoiceChannelId) : IRequest;

public sealed class StartMeetingRecordingCommandHandler(
    IDiscordMeetingOrchestrationService orchestrationService) : IRequestHandler<StartMeetingRecordingCommand>
{
    public Task Handle(StartMeetingRecordingCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return orchestrationService.StartMeetingRecordingAsync(
            request.MeetingId,
            request.GuildId,
            request.VoiceChannelId,
            cancellationToken);
    }
}

public sealed record MeetingEndedCommand(
    string MeetingId,
    string WhisperModel = "whisper-1",
    string SummaryModel = "llama3.1:8b",
    string? AdditionalSummaryInstructions = null) : IRequest<MeetingSummaryResult>;

public sealed class MeetingEndedCommandHandler(
    IDiscordMeetingOrchestrationService orchestrationService,
    IMeetingArtifactStore meetingArtifactStore,
    ITranscriptionService transcriptionService,
    ISummarizationService summarizationService,
    ILogger<MeetingEndedCommandHandler> logger)
    : IRequestHandler<MeetingEndedCommand, MeetingSummaryResult>
{
    public async Task<MeetingSummaryResult> Handle(MeetingEndedCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await orchestrationService.StopMeetingRecordingAsync(request.MeetingId, cancellationToken).ConfigureAwait(false);

        var audioFiles = await meetingArtifactStore
            .ListRecordedAudioFilesAsync(request.MeetingId, cancellationToken)
            .ConfigureAwait(false);

        if (audioFiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"No recorded WAV files were found for meeting '{request.MeetingId}'.");
        }

        logger.LogInformation(
            "Processing {AudioFileCount} recorded audio files for meeting {MeetingId}.",
            audioFiles.Count,
            request.MeetingId);

        var transcriptionResults = new ConcurrentDictionary<ulong, (RecordedAudioFile AudioFile, string Transcript, string TranscriptFilePath)>();
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Min(4, audioFiles.Count)
        };

        await Parallel.ForEachAsync(audioFiles, parallelOptions, async (audioFile, ct) =>
        {
            await using var audioStream = File.OpenRead(audioFile.FilePath);

            var transcription = await transcriptionService
                .TranscribeAsync(
                    new AudioTranscriptionRequest(
                        audioStream,
                        Path.GetFileName(audioFile.FilePath),
                        ContentType: "audio/wav",
                        Model: request.WhisperModel),
                    ct)
                .ConfigureAwait(false);

            var trimmedTranscript = transcription.Text.Trim();
            if (string.IsNullOrWhiteSpace(trimmedTranscript))
            {
                logger.LogWarning(
                    "Whisper returned an empty transcript for meeting {MeetingId}, user {UserId}.",
                    request.MeetingId,
                    audioFile.UserId);

                return;
            }

            var transcriptFilePath = await meetingArtifactStore
                .SaveTranscriptAsync(
                    request.MeetingId,
                    audioFile.UserId,
                    audioFile.Username,
                    trimmedTranscript,
                    ct)
                .ConfigureAwait(false);

            transcriptionResults[audioFile.UserId] = (audioFile, trimmedTranscript, transcriptFilePath);
        }).ConfigureAwait(false);

        if (transcriptionResults.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Whisper did not return any usable transcript content for meeting '{request.MeetingId}'.");
        }

        var transcriptFilesByUser = transcriptionResults.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.TranscriptFilePath);

        var transcriptSections = audioFiles
            .OrderBy(static file => file.Username, StringComparer.OrdinalIgnoreCase)
            .Where(file => transcriptionResults.ContainsKey(file.UserId))
            .Select(file =>
            {
                var transcriptionResult = transcriptionResults[file.UserId];
                return $"## {file.Username} ({file.UserId}){Environment.NewLine}{Environment.NewLine}{transcriptionResult.Transcript}";
            })
            .ToList();

        var consolidatedTranscript = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            transcriptSections);

        var summaryResult = await summarizationService
            .SummarizeAsync(
                new SummarizationRequest(
                    consolidatedTranscript,
                    request.SummaryModel,
                    request.AdditionalSummaryInstructions),
                cancellationToken)
            .ConfigureAwait(false);

        var markdown = BuildMeetingMarkdown(request.MeetingId, audioFiles, summaryResult.Summary);
        var summaryFilePath = await meetingArtifactStore
            .SaveSummaryAsync(request.MeetingId, markdown, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Meeting {MeetingId} summary generated successfully at {SummaryFilePath}.",
            request.MeetingId,
            summaryFilePath);

        return new MeetingSummaryResult(
            request.MeetingId,
            summaryFilePath,
            markdown,
            transcriptFilesByUser);
    }

    private static string BuildMeetingMarkdown(
        string meetingId,
        IReadOnlyCollection<RecordedAudioFile> audioFiles,
        string generatedSummary)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Meeting Summary - {meetingId}");
        builder.AppendLine();
        builder.AppendLine("## Participants");

        foreach (var audioFile in audioFiles.OrderBy(static file => file.Username, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {audioFile.Username} (`{audioFile.UserId}`)");
        }

        builder.AppendLine();
        builder.AppendLine(generatedSummary.Trim());

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }
}
