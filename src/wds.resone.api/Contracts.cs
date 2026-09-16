using System.Text.Json;
using System.Text.Json.Serialization;
using Wds.Resone.Api.Music;

namespace Wds.Resone.Api;
public sealed record Note(double Start, double Duration, int Pitch, int Velocity = 96);
public sealed class Lane
{
    public string Id { get; set; } = "melody";
    public string Name { get; set; } = "Melody";
    public int Bank { get; set; }
    public int Program { get; set; }
    public double Volume { get; set; } = .8;
    public bool Muted { get; set; }
    public bool Solo { get; set; }
    public bool Drums { get; set; }
    public bool IncludeInAi { get; set; } = true;
    public string Notation { get; set; } = "";
    public string OriginalBrief { get; set; } = "";
    public List<string> Prompts { get; set; } = [];
    public List<Note> Notes { get; set; } = [];
}

public sealed class SongProject
{
    public int Tempo { get; set; } = 120;
    public string Meter { get; set; } = "4/4";
    public int Bars { get; set; } = 16;
    public List<Lane> Lanes { get; set; } = [];
}

public sealed class ResoneSettings
{
    public string ApiUrl { get; set; } = "ws://127.0.0.1:8078/ws";
    public string LlmUrl { get; set; } = "http://127.0.0.1:8080/v1/chat/completions";
    public bool NativeInference { get; set; }
    public string NativeLibraryPath { get; set; } = "engines/llm/resone_inference.dll";
    public string NativeModelPath { get; set; } = "models/llm/gemma-4-E4B-it-Q6_K.gguf";
    public int ContextTokens { get; set; } = 32768;
    public int GpuLayers { get; set; } = 0;
    public bool AllowCpuFallback { get; set; } = true;
    public string LlmModel { get; set; } = "local-model";
    public string SttUrl { get; set; } = "http://127.0.0.1:8000/inference";
    public string TtsUrl { get; set; } = "http://127.0.0.1:8101/v1/audio/speech";
    public string TtsVoice { get; set; } = "default";
    public string TtsReferenceAudioPath { get; set; } = "";
    public string TtsReferenceText { get; set; } = "";
    public bool LogLlmRequests { get; set; }
    public string LlmLogDirectory { get; set; } = "logs";
    public int TimeoutSeconds { get; set; } = 240;
}

public sealed record Envelope(string Op, string RequestId, JsonElement Payload);
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = false)]
[JsonSerializable(typeof(Envelope))]
[JsonSerializable(typeof(ResoneSettings))]
[JsonSerializable(typeof(SongProject))]
[JsonSerializable(typeof(Lane))]
[JsonSerializable(typeof(List<Note>))]
[JsonSerializable(typeof(MusicCompositionRequest))]
[JsonSerializable(typeof(MusicCompositionInstructions))]
public partial class ResoneJson : JsonSerializerContext;
