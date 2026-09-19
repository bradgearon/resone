using System.Runtime.InteropServices;
using System.Text;
using Wds.Resone.Api.Ai;

namespace Wds.Resone.Api.VocalSinging;

public enum QwenTtsProfile { Base, VoiceDesign }

public sealed record QwenVoiceReferenceData(float[] SpeakerEmbedding, int[] Codes, int RefT, int NumCodebooks);

/// <summary>
/// Direct in-process qwentts.cpp binding pinned to commit
/// a8a7716b530e49fed537c57711247c12fbbb903c (public ABI 4).
/// Base and VoiceDesign checkpoints are independent contexts. Base is normally
/// retained for speech/vocal cloning; VoiceDesign is lazy and may be released
/// when the New Voice dialog closes. No HTTP TTS server or child process.
/// </summary>
public sealed class QwenTtsRuntime : IDisposable
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly object LoadLock = new();
    private static readonly Dictionary<QwenTtsProfile, QwenTtsRuntime> Shared = [];
    private static readonly Dictionary<QwenTtsProfile, string> SharedIdentity = [];

    private readonly ResoneSettings settings;
    private readonly QwenTtsProfile profile;
    private nint library;
    private nint context;
    private string modelType = "";
    private string[] voices = [];
    private bool disposed;

    [StructLayout(LayoutKind.Sequential)]
    private struct QtAudio { public nint Samples; public int NSamples; public int SampleRate; public int Channels; }

    [StructLayout(LayoutKind.Sequential)]
    private struct QtVoiceRef
    {
        public nint RefSpkEmb;
        public int RefSpkDim;
        public nint RefCodes;
        public int RefT;
        public int NumCodebooks;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct QtInitParams
    {
        public int AbiVersion;
        public nint TalkerPath;
        public nint CodecPath;
        public byte UseFa;
        public byte ClampFp16;
        public int MaxBatch;
        public float CodecChunkSec;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct QtTtsParams
    {
        public int AbiVersion;
        public nint Text;
        public nint Lang;
        public nint Instruct;
        public nint Speaker;
        public nint RefAudio24k;
        public int RefNSamples;
        public nint RefText;
        public long Seed;
        public int MaxNewTokens;
        public byte DoSample;
        public float Temperature;
        public int TopK;
        public float TopP;
        public float RepetitionPenalty;
        public byte SubtalkerDoSample;
        public float SubtalkerTemperature;
        public int SubtalkerTopK;
        public float SubtalkerTopP;
        public nint DumpDir;
        public nint Cancel;
        public nint CancelUserData;
        public nint OnChunk;
        public nint OnChunkUserData;
        public nint RefSpkEmb;
        public int RefSpkDim;
        public nint RefCodes;
        public int RefT;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void InitDefault(ref QtInitParams p);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint Init(ref QtInitParams p);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Free(nint q);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void TtsDefault(ref QtTtsParams p);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Synthesize(nint q, ref QtTtsParams p, ref QtAudio audio);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ExtractVoiceRef(nint q, nint samples, int sampleCount, ref QtVoiceRef voiceRef);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void VoiceRefFree(ref QtVoiceRef voiceRef);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NumCodebooks(nint q);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void AudioFree(ref QtAudio audio);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint LastError();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint Version();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SpeakerCount(nint q);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint SpeakerName(nint q, int index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private delegate bool CancelCallback(nint user);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void LogCallback(int level, nint message, nint user);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void LogSet(LogCallback? callback, nint user);

    private InitDefault initDefault = null!;
    private Init init = null!;
    private Free free = null!;
    private TtsDefault ttsDefault = null!;
    private Synthesize synthesize = null!;
    private ExtractVoiceRef extractVoiceRef = null!;
    private VoiceRefFree voiceRefFree = null!;
    private NumCodebooks numCodebooks = null!;
    private AudioFree audioFree = null!;
    private LastError lastError = null!;
    private Version version = null!;
    private SpeakerCount speakerCount = null!;
    private SpeakerName speakerName = null!;
    private LogSet logSet = null!;
    // qwentts.cpp logging is process-global. Keep one delegate rooted for the
    // process lifetime so releasing the lazy VoiceDesign context can never leave
    // native code holding a callback into a collected instance.
    private static readonly LogCallback SharedLogCallback = StaticLogCallback;

    private QwenTtsRuntime(ResoneSettings settings, QwenTtsProfile profile)
    {
        this.settings = settings;
        this.profile = profile;
    }

    public static Task<QwenTtsRuntime> SharedAsync(ResoneSettings settings, CancellationToken token, Action<string>? progress = null)
        => SharedAsync(settings, QwenTtsProfile.Base, token, progress);

    public static async Task<QwenTtsRuntime> SharedAsync(ResoneSettings settings, QwenTtsProfile profile, CancellationToken token, Action<string>? progress = null)
    {
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try { return await Task.Run(() => EnsureShared(settings, profile, progress), token).ConfigureAwait(false); }
        finally { Gate.Release(); }
    }

    public static async Task ReleaseAsync(QwenTtsProfile profile)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (LoadLock)
            {
                if (Shared.Remove(profile, out var runtime)) runtime.Dispose();
                SharedIdentity.Remove(profile);
            }
        }
        finally { Gate.Release(); }
    }

    public string ModelTypeName => modelType;
    public IReadOnlyList<string> Voices => voices;

    public async Task<string> SynthesizeWavAsync(string text, string voiceId, string outputPath, CancellationToken token)
    {
        ValidateText(text);
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var audio = await Task.Run(() => voices.Length > 0
                ? SynthesizeNamedBuffered(text, voiceId, token)
                : SynthesizeBuffered(text, token), token).ConfigureAwait(false);
            await WritePcm16WavAsync(outputPath, audio.Samples, audio.SampleRate, token).ConfigureAwait(false);
            return outputPath;
        }
        finally { Gate.Release(); }
    }

    public async Task<string> SynthesizeNamedVoiceWavAsync(string text, string voiceId, string outputPath, CancellationToken token)
    {
        ValidateText(text);
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var audio = await Task.Run(() => SynthesizeNamedBuffered(text, voiceId, token), token).ConfigureAwait(false);
            await WritePcm16WavAsync(outputPath, audio.Samples, audio.SampleRate, token).ConfigureAwait(false);
            return outputPath;
        }
        finally { Gate.Release(); }
    }

    public async Task<string> SynthesizeVoiceDesignWavAsync(string text, string instruction, string outputPath, CancellationToken token)
    {
        ValidateText(text);
        if (string.IsNullOrWhiteSpace(instruction) || instruction.Length > 2000)
            throw new ArgumentException("Describe the voice in 1–2000 characters.", nameof(instruction));
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var audio = await Task.Run(() => SynthesizeVoiceDesignBuffered(text, instruction.Trim(), token), token).ConfigureAwait(false);
            await WritePcm16WavAsync(outputPath, audio.Samples, audio.SampleRate, token).ConfigureAwait(false);
            return outputPath;
        }
        finally { Gate.Release(); }
    }

    public async Task<QwenVoiceReferenceData> ExtractVoiceReferenceAsync(string wavPath, CancellationToken token)
    {
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var audio = WavePcm.ReadMono24k(wavPath);
                if (audio.Samples.Length < 24000 / 2)
                    throw new InvalidDataException("Voice sample is too short. Use at least half a second of clear speech.");
                var pin = GCHandle.Alloc(audio.Samples, GCHandleType.Pinned);
                var native = new QtVoiceRef();
                try
                {
                    int rc = extractVoiceRef(context, pin.AddrOfPinnedObject(), audio.Samples.Length, ref native);
                    token.ThrowIfCancellationRequested();
                    if (rc != 0) throw new InvalidOperationException("Qwen voice reference extraction failed: " + Error());
                    if (native.RefSpkEmb == 0 || native.RefSpkDim <= 0 || native.RefCodes == 0 || native.RefT <= 0)
                        throw new InvalidDataException("Qwen returned an incomplete voice reference.");
                    var spk = new float[native.RefSpkDim]; Marshal.Copy(native.RefSpkEmb, spk, 0, spk.Length);
                    int k = native.NumCodebooks > 0 ? native.NumCodebooks : Math.Max(1, numCodebooks(context));
                    int codeCount = checked(k * native.RefT);
                    var codes = new int[codeCount]; Marshal.Copy(native.RefCodes, codes, 0, codes.Length);
                    return new QwenVoiceReferenceData(spk, codes, native.RefT, k);
                }
                finally
                {
                    if (native.RefSpkEmb != 0 || native.RefCodes != 0) voiceRefFree(ref native);
                    pin.Free();
                }
            }, token).ConfigureAwait(false);
        }
        finally { Gate.Release(); }
    }

    public async Task<string> SynthesizeReferenceWavAsync(string text, QwenVoiceReferenceData reference, string referenceText, string outputPath, CancellationToken token)
    {
        ValidateText(text);
        if (reference.SpeakerEmbedding.Length == 0 || reference.Codes.Length == 0 || reference.RefT <= 0 || reference.NumCodebooks <= 0)
            throw new ArgumentException("Saved voice reference is incomplete.", nameof(reference));
        if (reference.Codes.Length != checked(reference.RefT * reference.NumCodebooks))
            throw new ArgumentException("Saved voice RVQ dimensions are invalid.", nameof(reference));
        if (string.IsNullOrWhiteSpace(referenceText)) throw new ArgumentException("Saved voice transcript is required.", nameof(referenceText));

        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var audio = await Task.Run(() => SynthesizeReferenceBuffered(text, reference, referenceText, token), token).ConfigureAwait(false);
            await WritePcm16WavAsync(outputPath, audio.Samples, audio.SampleRate, token).ConfigureAwait(false);
            return outputPath;
        }
        finally { Gate.Release(); }
    }

    private (float[] Samples, int SampleRate) SynthesizeNamedBuffered(string text, string voiceId, CancellationToken token)
    {
        if (voices.Length == 0)
            throw new InvalidOperationException("The loaded Qwen checkpoint exposes no named speakers.");
        string selected = string.IsNullOrWhiteSpace(voiceId) || voiceId.Equals("default", StringComparison.OrdinalIgnoreCase) ? voices[0] : voiceId.Trim();
        if (!voices.Contains(selected, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Unknown Qwen voice '{selected}'. Available: {string.Join(", ", voices)}");
        return SynthesizeBuffered(text, token, (ref QtTtsParams p, Utf8Strings strings) => p.Speaker = strings.Add(selected));
    }

    private (float[] Samples, int SampleRate) SynthesizeVoiceDesignBuffered(string text, string instruction, CancellationToken token)
    {
        return SynthesizeBuffered(text, token, (ref QtTtsParams p, Utf8Strings strings) => p.Instruct = strings.Add(instruction));
    }

    private (float[] Samples, int SampleRate) SynthesizeReferenceBuffered(string text, QwenVoiceReferenceData reference, string referenceText, CancellationToken token)
    {
        var spkPin = GCHandle.Alloc(reference.SpeakerEmbedding, GCHandleType.Pinned);
        var codePin = GCHandle.Alloc(reference.Codes, GCHandleType.Pinned);
        try
        {
            return SynthesizeBuffered(text, token, (ref QtTtsParams p, Utf8Strings strings) =>
            {
                p.RefSpkEmb = spkPin.AddrOfPinnedObject();
                p.RefSpkDim = reference.SpeakerEmbedding.Length;
                p.RefCodes = codePin.AddrOfPinnedObject();
                p.RefT = reference.RefT;
                p.RefText = strings.Add(referenceText.Trim());
            });
        }
        finally { codePin.Free(); spkPin.Free(); }
    }

    private delegate void ConfigureTts(ref QtTtsParams p, Utf8Strings strings);

    private (float[] Samples, int SampleRate) SynthesizeBuffered(string text, CancellationToken token, ConfigureTts? configure = null)
    {
        token.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var p = new QtTtsParams(); ttsDefault(ref p);
        using var strings = new Utf8Strings();
        p.Text = strings.Add(text);
        p.Lang = strings.Add(settings.QwenTtsLanguage);
        configure?.Invoke(ref p, strings);
        CancelCallback cancel = _ => token.IsCancellationRequested;
        p.Cancel = Marshal.GetFunctionPointerForDelegate(cancel);
        var audio = new QtAudio();
        int rc = synthesize(context, ref p, ref audio);
        GC.KeepAlive(cancel);
        token.ThrowIfCancellationRequested();
        if (rc != 0) throw new InvalidOperationException("Qwen TTS failed: " + Error());
        if (audio.Samples == 0 || audio.NSamples <= 0 || audio.SampleRate <= 0 || audio.Channels != 1)
        {
            audioFree(ref audio);
            throw new InvalidDataException("Qwen TTS returned invalid audio.");
        }
        try
        {
            var samples = new float[audio.NSamples]; Marshal.Copy(audio.Samples, samples, 0, samples.Length);
            return (samples, audio.SampleRate);
        }
        finally { audioFree(ref audio); }
    }

    private static QwenTtsRuntime EnsureShared(ResoneSettings settings, QwenTtsProfile profile, Action<string>? progress)
    {
        lock (LoadLock)
        {
            string root = LlamaEngineResolver.Root, rid = LlamaEngineResolver.Rid;
            if (!settings.QwenTtsEngineDirectories.TryGetValue(rid, out var relative) || string.IsNullOrWhiteSpace(relative))
                throw new PlatformNotSupportedException($"No qwentts.cpp engine directory is configured for {rid}.");
            string engine = Path.GetFullPath(Path.IsPathRooted(relative) ? relative : Path.Combine(root, relative));
            string configuredTalker = profile == QwenTtsProfile.VoiceDesign ? settings.QwenTtsVoiceDesignTalkerPath : settings.QwenTtsTalkerPath;
            string talker = Path.GetFullPath(Path.IsPathRooted(configuredTalker) ? configuredTalker : Path.Combine(root, configuredTalker));
            string codec = Path.GetFullPath(Path.IsPathRooted(settings.QwenTtsCodecPath) ? settings.QwenTtsCodecPath : Path.Combine(root, settings.QwenTtsCodecPath));
            string id = string.Join('|', profile, rid, engine, talker, codec, settings.QwenTtsFlashAttention, settings.QwenTtsClampFp16, settings.QwenTtsMaxBatch, settings.QwenTtsCodecChunkSeconds);
            if (Shared.TryGetValue(profile, out var existing))
            {
                if (!string.Equals(SharedIdentity[profile], id, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Restart the Resone AI stack after changing Qwen TTS engine/model settings.");
                return existing;
            }
            if (!Directory.Exists(engine)) throw new DirectoryNotFoundException("Qwen TTS engine directory was not found: " + engine);
            if (!File.Exists(talker)) throw new FileNotFoundException(profile == QwenTtsProfile.VoiceDesign
                ? "Qwen VoiceDesign talker GGUF was not found. Configure qwenTtsVoiceDesignTalkerPath."
                : "Qwen Base talker GGUF was not found.", talker);
            if (!File.Exists(codec)) throw new FileNotFoundException("Qwen TTS codec GGUF was not found.", codec);

            progress?.Invoke(profile == QwenTtsProfile.VoiceDesign ? "Voice Designer · Loading Qwen VoiceDesign model…" : "Vocals · Loading local Qwen Base TTS model…");
            ValidateManagedAbiLayout();
            var runtime = new QwenTtsRuntime(settings, profile);
            try
            {
                // qwentts.cpp/ggml loads backend DLLs while qt_init runs. Scope the
                // legacy Windows DLL directory override to initialization only and
                // restore the prior process setting immediately afterwards so Qwen
                // cannot disturb FluidSynth or any other native subsystem.
                using var dllSearch = DllDirectoryScope.Enter(engine);
                runtime.LoadLibrary(engine); runtime.Bind();
                string runtimeVersion = PtrUtf8(runtime.version());
                string expectedVersion = string.IsNullOrWhiteSpace(settings.QwenTtsExpectedVersionPrefix) ? "a8a7716" : settings.QwenTtsExpectedVersionPrefix.Trim();
                if (!runtimeVersion.StartsWith(expectedVersion, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"qwentts.cpp runtime mismatch. Resone is pinned to commit {expectedVersion} (ABI 4), but the loaded DLL reports '{runtimeVersion}'.");
                runtime.InstallLogCallback();
                var initParams = new QtInitParams(); runtime.initDefault(ref initParams);
                using var strings = new Utf8Strings();
                initParams.TalkerPath = strings.Add(talker); initParams.CodecPath = strings.Add(codec);
                initParams.UseFa = settings.QwenTtsFlashAttention ? (byte)1 : (byte)0;
                initParams.ClampFp16 = settings.QwenTtsClampFp16 ? (byte)1 : (byte)0;
                initParams.MaxBatch = Math.Max(1, settings.QwenTtsMaxBatch);
                initParams.CodecChunkSec = settings.QwenTtsCodecChunkSeconds <= 0 ? 24f : settings.QwenTtsCodecChunkSeconds;
                runtime.context = runtime.init(ref initParams);
                if (runtime.context == 0) throw new InvalidOperationException("Could not initialize qwentts.cpp: " + runtime.Error());
                int count = Math.Max(0, runtime.speakerCount(runtime.context));
                runtime.voices = Enumerable.Range(0, count).Select(i => PtrUtf8(runtime.speakerName(runtime.context, i))).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                string talkerName = Path.GetFileName(talker);
                runtime.modelType = runtime.voices.Length > 0 ? "custom_voice"
                    : talkerName.Contains("voicedesign", StringComparison.OrdinalIgnoreCase) || talkerName.Contains("voice-design", StringComparison.OrdinalIgnoreCase) ? "voice_design"
                    : talkerName.Contains("base", StringComparison.OrdinalIgnoreCase) ? "base" : "unknown";
                if (profile == QwenTtsProfile.VoiceDesign && runtime.modelType != "voice_design")
                    throw new InvalidOperationException($"Configured VoiceDesign checkpoint does not appear to be a voice_design model: {talkerName}");
                Shared[profile] = runtime; SharedIdentity[profile] = id;
                progress?.Invoke(profile == QwenTtsProfile.VoiceDesign ? "Voice Designer · Qwen VoiceDesign ready." : $"Vocals · Qwen Base ready ({runtime.modelType}).");
                runtime.Log($"loaded profile={profile}; qwentts.cpp={runtimeVersion}; abi=4; engine={engine}; talker={talker}; codec={codec}; modelType={runtime.modelType}");
                return runtime;
            }
            catch { runtime.Dispose(); throw; }
        }
    }

    private static void ValidateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 12000) throw new ArgumentException("Vocal text must be 1–12000 characters.", nameof(text));
    }

    private static void ValidateManagedAbiLayout()
    {
        if (IntPtr.Size != 8) throw new PlatformNotSupportedException("Resone's qwentts.cpp binding currently supports 64-bit engine packs only.");
        if (Marshal.SizeOf<QtInitParams>() != 40 || Marshal.OffsetOf<QtInitParams>(nameof(QtInitParams.MaxBatch)).ToInt32() != 28 ||
            Marshal.SizeOf<QtTtsParams>() != 184 || Marshal.OffsetOf<QtTtsParams>(nameof(QtTtsParams.Temperature)).ToInt32() != 80 ||
            Marshal.OffsetOf<QtTtsParams>(nameof(QtTtsParams.SubtalkerTemperature)).ToInt32() != 100 ||
            Marshal.OffsetOf<QtTtsParams>(nameof(QtTtsParams.Cancel)).ToInt32() != 120 || Marshal.SizeOf<QtVoiceRef>() != 32)
            throw new InvalidOperationException("Resone's managed qwentts.cpp ABI layout does not match pinned public ABI 4.");
    }

    private void LoadLibrary(string engine)
    {
        // The public ABI at a8a7716 is exported by the qwen shared library. Do not
        // enumerate/probe every DLL in the engine directory: attempting to load a
        // GGML backend DLL directly can make the Windows loader show an Entry Point
        // Not Found dialog before managed code has a chance to report the real pack
        // mismatch. The engine pack is a single atomic native unit.
        string configured = string.IsNullOrWhiteSpace(settings.QwenTtsLibraryName)
            ? (OperatingSystem.IsWindows() ? "qwen.dll" : OperatingSystem.IsMacOS() ? "libqwen.dylib" : "libqwen.so")
            : settings.QwenTtsLibraryName.Trim();
        string path = Path.Combine(engine, configured);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Pinned qwentts.cpp shared library was not found. Expected '{configured}' in {engine}.", path);

        if (OperatingSystem.IsWindows()) ValidateWindowsNativePack(engine);

        try
        {
            library = OperatingSystem.IsWindows()
                ? LoadWindowsLibraryIsolated(path)
                : NativeLibrary.Load(path);
        }
        catch (Exception ex)
        {
            throw new DllNotFoundException(
                $"Could not load qwentts.cpp from '{path}'. Resone requires one self-consistent native pack built from qwentts.cpp " +
                $"a8a7716b530e49fed537c57711247c12fbbb903c (ABI 4) and its recursively pinned GGML submodule. " +
                "Do not mix qwen.dll or ggml*.dll files from another qwentts/llama/whisper build.", ex);
        }

        if (!NativeLibrary.TryGetExport(library, "qt_init", out _) || !NativeLibrary.TryGetExport(library, "qt_synthesize", out _))
        {
            NativeLibrary.Free(library); library = 0;
            throw new EntryPointNotFoundException($"'{path}' is not the qwentts.cpp ABI-4 shared library: qt_init/qt_synthesize were not exported.");
        }
        Log("qwentts.cpp shared library: " + path);
    }

    private static void ValidateWindowsNativePack(string engine)
    {
        // a8a7716's GGML build exposes q2_0 reference quantization from the CPU
        // backend. A qwen DLL compiled against that tree cannot run with an older
        // ggml-cpu.dll that lacks this export. This exact mismatch otherwise causes
        // the modal Windows error: quantize_row_q2_0_ref could not be located.
        string[] required = ["qwen.dll", "ggml.dll", "ggml-base.dll", "ggml-cpu.dll", "ggml-cuda.dll"];
        var missing = required.Where(x => !File.Exists(Path.Combine(engine, x))).ToArray();
        if (missing.Length > 0)
            throw new FileNotFoundException($"Qwen NVIDIA engine pack is incomplete. Missing: {string.Join(", ", missing)}. Rebuild/copy the complete a8a7716 pack as one directory.");

        string qwen = Path.Combine(engine, "qwen.dll");
        foreach (string export in new[] { "qt_version", "qt_init", "qt_synthesize", "qt_extract_voice_ref", "qt_voice_ref_free", "qt_num_codebooks" })
            if (!PortableExecutableExports.Contains(qwen, export))
                throw new InvalidOperationException($"The Qwen engine qwen.dll is not the pinned ABI-4 shared library: export '{export}' is missing. Build QWEN_SHARED=ON from a8a7716.");

        string cpu = Path.Combine(engine, "ggml-cpu.dll");
        // Parse the export table directly. Do not LoadLibrary/LoadLibraryEx the
        // backend even for validation: that could recreate the modal Windows
        // dependency error we are specifically trying to replace with a normal
        // Resone error message.
        if (!PortableExecutableExports.Contains(cpu, "quantize_row_q2_0_ref"))
            throw new InvalidOperationException(
                "The Qwen engine directory contains a stale/incompatible ggml-cpu.dll: it does not export quantize_row_q2_0_ref. " +
                "qwen.dll and every ggml*.dll must come from the same qwentts.cpp a8a7716 build (including that commit's recursive GGML submodule). " +
                "Delete the existing Qwen engine directory before staging the rebuilt pack; do not overlay newer DLLs onto older ones.");
    }

    private static nint LoadWindowsLibraryIsolated(string path)
    {
        nint h = LoadLibraryEx(path, 0, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        if (h == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Windows could not load the pinned Qwen native library and its local dependencies.");
        return h;
    }

    private void Bind()
    {
        initDefault = Export<InitDefault>("qt_init_default_params"); init = Export<Init>("qt_init"); free = Export<Free>("qt_free");
        ttsDefault = Export<TtsDefault>("qt_tts_default_params"); synthesize = Export<Synthesize>("qt_synthesize");
        extractVoiceRef = Export<ExtractVoiceRef>("qt_extract_voice_ref"); voiceRefFree = Export<VoiceRefFree>("qt_voice_ref_free"); numCodebooks = Export<NumCodebooks>("qt_num_codebooks");
        audioFree = Export<AudioFree>("qt_audio_free"); lastError = Export<LastError>("qt_last_error"); version = Export<Version>("qt_version");
        speakerCount = Export<SpeakerCount>("qt_n_speakers"); speakerName = Export<SpeakerName>("qt_speaker_name"); logSet = Export<LogSet>("qt_log_set");
    }

    private void InstallLogCallback() => logSet(SharedLogCallback, 0);

    private static void StaticLogCallback(int level, nint message, nint _)
    {
        string text = PtrUtf8(message);
        if (text.Length == 0 || level < 1) return;
        StaticLog($"native[{level}] {text}");
    }

    private T Export<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
    private string Error() { string error = PtrUtf8(lastError()); return string.IsNullOrWhiteSpace(error) ? "qwentts.cpp returned an unknown error." : error; }
    private static string PtrUtf8(nint value) => value == 0 ? "" : Marshal.PtrToStringUTF8(value) ?? "";
    private void ThrowIfDisposed() { if (disposed || context == 0) throw new ObjectDisposedException(nameof(QwenTtsRuntime)); }

    private void Log(string message) => StaticLog(message);

    private static void StaticLog(string message)
    {
        try
        {
            string dir = Path.Combine(LlamaEngineResolver.Root, "logs"); Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "qwen-tts.log"), $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch { }
    }

    private sealed class Utf8Strings : IDisposable
    {
        private readonly List<nint> values = [];
        public nint Add(string? value) { if (string.IsNullOrEmpty(value)) return 0; nint p = Marshal.StringToCoTaskMemUTF8(value); values.Add(p); return p; }
        public void Dispose() { foreach (var p in values) Marshal.FreeCoTaskMem(p); }
    }

    private static async Task WritePcm16WavAsync(string path, float[] samples, int sampleRate, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllBytesAsync(path, WavePcm.WritePcm16Wav(samples, sampleRate), token).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (disposed) return; disposed = true;
        try { if (context != 0) free(context); } catch { }
        context = 0;
        try { if (library != 0) NativeLibrary.Free(library); } catch { }
        library = 0;
        GC.SuppressFinalize(this);
    }

    private const uint LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100;
    private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

    // Minimal PE export reader used only for preflight. It never maps executable
    // code and therefore cannot trigger dependency resolution or DllMain.
    private static class PortableExecutableExports
    {
        public static bool Contains(string path, string exportName)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: true);
            if (br.ReadUInt16() != 0x5A4D) return false; // MZ
            fs.Position = 0x3C; int pe = br.ReadInt32();
            if (pe <= 0 || pe > fs.Length - 256) return false;
            fs.Position = pe;
            if (br.ReadUInt32() != 0x00004550) return false; // PE\0\0
            _ = br.ReadUInt16(); ushort sections = br.ReadUInt16();
            fs.Position += 12; ushort optionalSize = br.ReadUInt16(); fs.Position += 2;
            long optional = fs.Position; ushort magic = br.ReadUInt16();
            int dataDirectoryOffset = magic == 0x20B ? 112 : magic == 0x10B ? 96 : -1;
            if (dataDirectoryOffset < 0 || optionalSize < dataDirectoryOffset + 8) return false;
            fs.Position = optional + dataDirectoryOffset;
            uint exportRva = br.ReadUInt32(); uint exportSize = br.ReadUInt32();
            if (exportRva == 0 || exportSize == 0) return false;
            long sectionTable = optional + optionalSize;
            var map = new List<(uint Va,uint Vs,uint Raw,uint RawSize)>();
            fs.Position = sectionTable;
            for (int i = 0; i < sections; i++)
            {
                fs.Position += 8; uint vs = br.ReadUInt32(); uint va = br.ReadUInt32(); uint rawSize = br.ReadUInt32(); uint raw = br.ReadUInt32();
                fs.Position += 16; map.Add((va, vs, raw, rawSize));
            }
            long Rva(uint rva)
            {
                foreach (var x in map)
                {
                    uint span = Math.Max(x.Vs, x.RawSize);
                    if (rva >= x.Va && rva < x.Va + span) return x.Raw + (rva - x.Va);
                }
                return -1;
            }
            long exp = Rva(exportRva); if (exp < 0) return false;
            fs.Position = exp + 24; uint numberOfNames = br.ReadUInt32();
            fs.Position += 4; uint addressOfNames = br.ReadUInt32();
            long names = Rva(addressOfNames); if (names < 0 || numberOfNames > 100000) return false;
            for (uint i = 0; i < numberOfNames; i++)
            {
                fs.Position = names + i * 4; uint nameRva = br.ReadUInt32(); long off = Rva(nameRva); if (off < 0) continue;
                fs.Position = off; var bytes = new List<byte>(64); byte b;
                while (fs.Position < fs.Length && bytes.Count < 512 && (b = br.ReadByte()) != 0) bytes.Add(b);
                if (Encoding.ASCII.GetString(bytes.ToArray()).Equals(exportName, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }

    private sealed class DllDirectoryScope : IDisposable
    {
        private readonly string? previous;
        private readonly bool changed;
        private DllDirectoryScope(string? previous, bool changed) { this.previous = previous; this.changed = changed; }

        public static DllDirectoryScope Enter(string directory)
        {
            if (!OperatingSystem.IsWindows()) return new DllDirectoryScope(null, false);
            uint needed = GetDllDirectory(0, null);
            string previous = "";
            if (needed > 0)
            {
                var buffer = new StringBuilder(checked((int)needed + 1));
                GetDllDirectory((uint)buffer.Capacity, buffer);
                previous = buffer.ToString();
            }
            if (!SetDllDirectory(directory))
                throw new InvalidOperationException($"Could not add the Qwen engine directory to the Windows DLL search path: {Marshal.GetLastWin32Error()}.");
            return new DllDirectoryScope(previous, true);
        }

        public void Dispose()
        {
            if (!changed) return;
            // Empty restores the normal search path; a non-empty value restores the
            // directory another native subsystem had explicitly configured.
            SetDllDirectory(previous ?? "");
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadLibraryEx(string lpFileName, nint hFile, uint dwFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetDllDirectory(uint nBufferLength, StringBuilder? lpBuffer);
}
