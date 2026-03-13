using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using record_ai_discord_bot.Application.Commands;
using record_ai_discord_bot.Application.Services;

namespace record_ai_discord_bot.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    IServiceScopeFactory serviceScopeFactory,
    IDiscordMeetingOrchestrationService orchestrationService,
    IOptions<MeetingAutomationOptions> automationOptions)
    : BackgroundService
{
    private readonly MeetingAutomationOptions _options = automationOptions.Value;
    private bool _recordingStarted;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Discord recording worker starting.");

        await orchestrationService.StartAsync(stoppingToken).ConfigureAwait(false);

        if (_options.StartRecordingOnStartup)
        {
            ValidateAutomationOptions(_options);

            await SendAsync(
                    new StartMeetingRecordingCommand(_options.MeetingId, _options.GuildId, _options.VoiceChannelId),
                    stoppingToken)
                .ConfigureAwait(false);

            _recordingStarted = true;

            logger.LogInformation(
                "Auto-started recording for meeting {MeetingId} in guild {GuildId}, channel {VoiceChannelId}.",
                _options.MeetingId,
                _options.GuildId,
                _options.VoiceChannelId);
        }
        else
        {
            logger.LogInformation(
                "Worker is ready. Set {Section}:{Property}=true to auto-start a meeting when the service boots.",
                MeetingAutomationOptions.SectionName,
                nameof(MeetingAutomationOptions.StartRecordingOnStartup));
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Discord recording worker cancellation requested.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_recordingStarted)
            {
                try
                {
                    if (_options.GenerateSummaryOnShutdown)
                    {
                        var result = await SendAsync(
                                new MeetingEndedCommand(
                                    _options.MeetingId,
                                    _options.WhisperModel,
                                    _options.SummaryModel,
                                    _options.AdditionalSummaryInstructions),
                                cancellationToken)
                            .ConfigureAwait(false);

                        logger.LogInformation(
                            "Meeting summary generated for {MeetingId} at {SummaryFilePath}.",
                            result.MeetingId,
                            result.SummaryFilePath);
                    }
                    else
                    {
                        await orchestrationService.StopMeetingRecordingAsync(_options.MeetingId, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Failed while finalizing meeting {MeetingId}. Falling back to orchestration shutdown.",
                        _options.MeetingId);

                    await orchestrationService.StopMeetingRecordingAsync(_options.MeetingId, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        finally
        {
            await orchestrationService.StopAsync(cancellationToken).ConfigureAwait(false);
            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ValidateAutomationOptions(MeetingAutomationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.MeetingId))
        {
            throw new InvalidOperationException($"{MeetingAutomationOptions.SectionName}:MeetingId is required.");
        }

        if (options.GuildId == 0)
        {
            throw new InvalidOperationException($"{MeetingAutomationOptions.SectionName}:GuildId must be configured.");
        }

        if (options.VoiceChannelId == 0)
        {
            throw new InvalidOperationException($"{MeetingAutomationOptions.SectionName}:VoiceChannelId must be configured.");
        }
    }

    private async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendAsync(IRequest request, CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(request, cancellationToken).ConfigureAwait(false);
    }
}
