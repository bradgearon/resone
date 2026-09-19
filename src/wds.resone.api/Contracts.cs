using System.Text.Json;
using System.Text.Json.Serialization;
using Wds.Resone.Api.Music;
using Wds.Resone.Api.VocalSinging;

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
    /// <summary>True when this pitched lane represents a sung vocal melody.</summary>
    public bool Vocals { get; set; }
    public bool IncludeInAi { get; set; } = true;
    public string Lyrics { get; set; } = "";
    public string VoiceId { get; set; } = "default";
    public string RenderedVocalPath { get; set; } = "";
    public string RenderedVocalSignature { get; set; } = "";
    public List<VocalGuidanceEvent> VocalGuidance { get; set; } = [];
    public string Notation { get; set; } = "";
    public string OriginalBrief { get; set; } = "";
    /// <summary>Exact imported/generated clip span in quarter-note beats. Zero means derive it from Notes.</summary>
    public double ClipLengthBeats { get; set; }
    public string ImportedMidiName { get; set; } = "";
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
    // Backward-compatible native inference switch. Older builds/configs used
    // nativeInference; newer/user-facing configs may use useLocalInference.
    // Either flag opts into the in-process GGUF adapter.
    public bool NativeInference { get; set; }
    public bool UseLocalInference { get; set; }
    [JsonIgnore]
    public bool LocalInferenceEnabled => NativeInference || UseLocalInference;
    // The actual inference engine is the downloaded llama.cpp runtime selected by platform.
    // The small Resone C++ ABI bridge is an application implementation detail and is not configurable.
    public Dictionary<string, string> LlamaEngineDirectories { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["win-x64"] = "engines/llm/llama-cpp-dynamic-win-x64",
        ["win-arm64"] = "engines/llm/llama-cpp-dynamic-win-arm64",
        ["linux-x64"] = "engines/llm/llama-cpp-dynamic-linux-x64",
        ["linux-arm64"] = "engines/llm/llama-cpp-dynamic-linux-arm64",
        ["osx-x64"] = "engines/llm/llama-cpp-dynamic-osx-x64",
        ["osx-arm64"] = "engines/llm/llama-cpp-dynamic-osx-arm64"
    };
    public string NativeModelPath { get; set; } = "models/llm/gemma-4-E4B-it-Q6_K.gguf";
    // Match Resone's established local llama.cpp inference configuration.
    public int ContextTokens { get; set; } = 16384;
    public int GpuLayers { get; set; } = 99;
    public bool AllowCpuFallback { get; set; } = true;
    public bool FlashAttention { get; set; } = true;
    public bool ReasoningEnabled { get; set; } = false;
    public bool WarmModelOnStackStart { get; set; } = true;
    public float Temperature { get; set; } = .7f;
    public int TopK { get; set; } = 40;
    public float TopP { get; set; } = .95f;
    public string LlmModel { get; set; } = "local-model";
    // Spoken composition prompts use the bundled whisper.cpp engine on supported desktop platforms.
    // The worker lazily starts/warm-loads this runtime when recording begins; SttUrl is retained only
    // as a compatibility fallback for non-Windows/test environments.
    public bool UseLocalWhisper { get; set; } = true;
    public Dictionary<string, string> WhisperEngineDirectories { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["win-x64"] = "engines/asr/whispercpp-nvidia-win-x64",
        ["win-arm64"] = "engines/asr/whispercpp-win-arm64",
        ["linux-x64"] = "engines/asr/whispercpp-linux-x64",
        ["linux-arm64"] = "engines/asr/whispercpp-linux-arm64",
        ["osx-x64"] = "engines/asr/whispercpp-osx-x64",
        ["osx-arm64"] = "engines/asr/whispercpp-osx-arm64"
    };
    public string WhisperServerName { get; set; } = "whisper-server.exe";
    public string WhisperModelPath { get; set; } = "models/asr/ggml-large-v3-turbo.bin";
    public string WhisperLanguage { get; set; } = "en";
    public string SttUrl { get; set; } = "http://127.0.0.1:8000/inference";
    public string TtsVoice { get; set; } = "default";
    public string TtsReferenceAudioPath { get; set; } = "";
    public string TtsReferenceText { get; set; } = "";
    // qwentts.cpp server configuration used by Voice Designer and the Vocals lane. The engine is
    // supplied as a prebuilt platform pack containing qwen-server.exe/tts-server.exe.
    public Dictionary<string, string> QwenTtsEngineDirectories { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["win-x64"] = "engines/tts/qwenttscpp-nvidia-win-x64",
        ["win-arm64"] = "engines/tts/qwentts-cpp-dynamic-win-arm64",
        ["linux-x64"] = "engines/tts/qwentts-cpp-dynamic-linux-x64",
        ["linux-arm64"] = "engines/tts/qwentts-cpp-dynamic-linux-arm64",
        ["osx-x64"] = "engines/tts/qwentts-cpp-dynamic-osx-x64",
        ["osx-arm64"] = "engines/tts/qwentts-cpp-dynamic-osx-arm64"
    };
    // qwentts.cpp normal CUDA builds ship the server/CLI tools with the public ABI
    // statically linked. Resone therefore manages qwen-server.exe (or tts-server.exe)
    // as a private loopback service instead of requiring qwen.dll.
    // Legacy compatibility settings retained so older branches/files that still reference the
    // former in-process DLL loader continue to compile. The current server-managed Qwen path
    // does not use either value; QwenTtsServiceManager launches qwen-server.exe instead.
    public string QwenTtsExpectedVersionPrefix { get; set; } = "v0.6";
    public string QwenTtsLibraryName { get; set; } = "qwen-tts.dll";
    public string QwenTtsServerName { get; set; } = "qwen-server.exe";
    public int QwenTtsStartupTimeoutSeconds { get; set; } = 120;
    // Idle shutdown delays in milliseconds. Voice Designer is pinned while its dialog
    // is open; otherwise each activity refreshes the corresponding deadline.
    public Dictionary<string, int> ServiceDelays { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["voice-design"] = 3000,
        ["custom-voice"] = 3000
    };
    public string QwenTtsTalkerPath { get; set; } = "models/tts/qwen-talker-0.6b-base-Q8_0.gguf";
    // VoiceDesign is a separate 1.7B checkpoint. It is lazy-loaded only by the New Voice workflow.
    public string QwenTtsVoiceDesignTalkerPath { get; set; } = "models/tts/qwen-talker-1.7b-voicedesign-Q8_0.gguf";
    public string QwenTtsCodecPath { get; set; } = "models/tts/qwen-tokenizer-12hz-Q8_0.gguf";
    public string QwenTtsLanguage { get; set; } = "English";
    public bool QwenTtsFlashAttention { get; set; } = true;
    public bool QwenTtsClampFp16 { get; set; }
    public int QwenTtsMaxBatch { get; set; } = 1;
    public float QwenTtsCodecChunkSeconds { get; set; } = 24f;
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
[JsonSerializable(typeof(SongGenerationState))]
[JsonSerializable(typeof(SongSectionMemory))]
[JsonSerializable(typeof(SongMusicalMemory))]
[JsonSerializable(typeof(VocalGuidanceEvent))]
[JsonSerializable(typeof(List<VocalGuidanceEvent>))]
[JsonSerializable(typeof(VoiceLibraryDocument))]
[JsonSerializable(typeof(SavedVoice))]
[JsonSerializable(typeof(VoicePitchProfile))]
[JsonSerializable(typeof(VoicePreviewRecord))]
[JsonSerializable(typeof(SongWorkspaceIndex))]
[JsonSerializable(typeof(SongWorkspaceSummary))]
[JsonSerializable(typeof(SongWorkspaceMeta))]
[JsonSerializable(typeof(SongWorkspaceHistoryEntry))]
[JsonSerializable(typeof(List<SongWorkspaceHistoryEntry>))]
public partial class ResoneJson : JsonSerializerContext;
