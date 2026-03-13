var builder = DistributedApplication.CreateBuilder(args);

// Add container resources for Whisper (transcription) and Ollama (summarization)
var whisper = builder
    .AddContainer("whisper", "aarnphm/whispercpp")
    .WithHttpEndpoint(8000, 8000, name: "whisper-http");

var ollama = builder
    .AddContainer("ollama", "ollama/ollama")
    .WithHttpEndpoint(11434, 11434, name: "ollama-http");

// Add the Worker Service that orchestrates the recording pipeline
var worker = builder
    .AddProject<Projects.record_ai_discord_bot_Worker>("worker")
    .WaitFor(whisper)
    .WaitFor(ollama);

// Keep existing services
var cache = builder.AddRedis("cache");

var apiService = builder.AddProject<Projects.record_ai_discord_bot_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.record_ai_discord_bot_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
