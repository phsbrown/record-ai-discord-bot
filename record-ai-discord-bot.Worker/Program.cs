using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using record_ai_discord_bot.Application.Commands;
using record_ai_discord_bot.Infrastructure.Discord;
using record_ai_discord_bot.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.Configure<MeetingAutomationOptions>(
    builder.Configuration.GetSection(MeetingAutomationOptions.SectionName));

builder.Services.AddDiscordRecordingInfrastructure(builder.Configuration);

builder.Services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssemblyContaining<MeetingEndedCommandHandler>();
});

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromMinutes(15);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
