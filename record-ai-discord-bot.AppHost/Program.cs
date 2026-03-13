var builder = DistributedApplication.CreateBuilder(args);

var recordingsPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".data", "recordings"));
var ollamaDataPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".data", "ollama"));

Directory.CreateDirectory(recordingsPath);
Directory.CreateDirectory(ollamaDataPath);

var whisper = builder.AddContainer("whisper", "localai/localai", "latest-cpu")
    .WithHttpEndpoint(port: 8000, targetPort: 8080, name: "http")
    .WithBindMount(recordingsPath, "/recordings");

var ollama = builder.AddContainer("ollama", "ollama/ollama", "latest")
    .WithEnvironment("OLLAMA_HOST", "0.0.0.0:11434")
    .WithHttpEndpoint(port: 11434, targetPort: 11434, name: "http")
    .WithBindMount(ollamaDataPath, "/root/.ollama")
    .WithBindMount(recordingsPath, "/recordings");

builder.AddProject<Projects.record_ai_discord_bot_Worker>("worker")
    .WithEnvironment("AudioPersistence__RecordingsRootPath", recordingsPath)
    .WithReference(whisper.GetEndpoint("http"))
    .WaitFor(whisper)
    .WithReference(ollama.GetEndpoint("http"))
    .WaitFor(ollama);

builder.Build().Run();
