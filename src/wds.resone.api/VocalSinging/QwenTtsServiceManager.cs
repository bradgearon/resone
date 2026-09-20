using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;

namespace Wds.Resone.Api.VocalSinging;

public enum QwenTtsServiceKind
{
    VoiceDesign,
    CustomVoice
}

/// <summary>
/// Owns the qwentts.cpp HTTP server processes used by Resone.
///
/// qwentts.cpp's normal CUDA build links the public ABI statically into the CLI/server
/// tools, so Resone talks to qwen-server/tts-server instead of assuming qwen.dll exists.
/// The two logical services are loaded lazily and released after their configured idle
/// delay. Only one GPU-resident Qwen service is kept alive at a time; switching modes
/// stops an idle service before loading the other model.
///
/// "custom-voice" is Resone's saved/reference-voice service. qwentts.cpp performs
/// arbitrary reference voice cloning with a Base checkpoint, so this slot intentionally
/// uses QwenTtsTalkerPath and the server's /v1/audio/voices registry.
/// </summary>
public sealed class QwenTtsServiceManager : IAsyncDisposable
{
    private sealed class Slot(QwenTtsServiceKind kind)
    {
        public QwenTtsServiceKind Kind { get; } = kind;
        public Process? Process { get; set; }
        public int Port { get; set; }
        public bool ScopeActive { get; set; }
        public int ActiveRequests { get; set; }
        public DateTimeOffset LastActivityUtc { get; set; } = DateTimeOffset.MinValue;
        public CancellationTokenSource? IdleStop { get; set; }
        public HashSet<string> RegisteredVoiceIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public ConcurrentQueue<string> Output { get; } = new();
        public bool IsRunning => Process is { HasExited: false };
    }

    private readonly ResoneSettings settings;
    private readonly string aiRoot;
    private readonly HttpClient http;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<QwenTtsServiceKind, Slot> slots = new()
    {
        [QwenTtsServiceKind.VoiceDesign] = new(QwenTtsServiceKind.VoiceDesign),
        [QwenTtsServiceKind.CustomVoice] = new(QwenTtsServiceKind.CustomVoice)
    };
    private bool disposed;
    private bool runtimeEnsured;

    public QwenTtsServiceManager(ResoneSettings settings, string aiRoot, HttpClient http)
    {
        this.settings = settings;
        this.aiRoot = Path.GetFullPath(aiRoot);
        this.http = http;
    }

    /// <summary>Marks a UI scope such as the New Voice dialog active/inactive.</summary>
    public async Task SetScopeActiveAsync(QwenTtsServiceKind kind, bool active, CancellationToken token = default, Func<string, Task>? progress = null)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var slot = slots[kind];
            slot.ScopeActive = active;
            TouchLocked(slot);
            // Opening the voice designer should not download the TTS runtime. The first actual
            // synthesis request provisions it; after that, scope activation may warm it normally.
            if (active && runtimeEnsured)
                await EnsureStartedLockedAsync(slot, token, progress).ConfigureAwait(false);
            else if (!active)
                ScheduleIdleStopLocked(slot);
        }
        finally { gate.Release(); }
    }

    /// <summary>One-shot activity signal. Starts the service if needed and refreshes its idle deadline.</summary>
    public async Task ActivityAsync(QwenTtsServiceKind kind, CancellationToken token = default, Func<string, Task>? progress = null)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var slot = slots[kind];
            TouchLocked(slot);
            // Lane/voice selection is only a warm hint. Never trigger a large first-use download
            // until the user actually asks Resone to synthesize speech/voice audio.
            if (runtimeEnsured)
                await EnsureStartedLockedAsync(slot, token, progress).ConfigureAwait(false);
            ScheduleIdleStopLocked(slot);
        }
        finally { gate.Release(); }
    }

    public async Task<string> SynthesizeVoiceDesignWavAsync(string text, string instruction, string outputPath, CancellationToken token, Func<string, Task>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("VoiceDesign text is empty.", nameof(text));
        if (string.IsNullOrWhiteSpace(instruction)) throw new ArgumentException("VoiceDesign instruction is empty.", nameof(instruction));
        byte[] wav = await WithServiceAsync(QwenTtsServiceKind.VoiceDesign, token, progress, async (slot, ct) =>
        {
            var body = new JsonObject
            {
                ["input"] = text.Trim(),
                ["instructions"] = instruction.Trim(),
                ["response_format"] = "wav"
            };
            return await PostForBytesAsync(slot, "/v1/audio/speech", body, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        await File.WriteAllBytesAsync(outputPath, wav, token).ConfigureAwait(false);
        return outputPath;
    }

    public async Task<string> SynthesizeDefaultWavAsync(string text, string voiceId, string outputPath, CancellationToken token, Func<string, Task>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Speech text is empty.", nameof(text));
        byte[] wav = await WithServiceAsync(QwenTtsServiceKind.CustomVoice, token, progress, async (slot, ct) =>
        {
            var body = new JsonObject
            {
                ["input"] = text.Trim(),
                ["response_format"] = "wav"
            };
            if (!string.IsNullOrWhiteSpace(voiceId) && !voiceId.Equals("default", StringComparison.OrdinalIgnoreCase))
                body["voice"] = voiceId.Trim();
            return await PostForBytesAsync(slot, "/v1/audio/speech", body, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        await File.WriteAllBytesAsync(outputPath, wav, token).ConfigureAwait(false);
        return outputPath;
    }

    public async Task<string> SynthesizeSavedVoiceWavAsync(string text, SavedVoice voice, string samplePath, string outputPath, CancellationToken token, Func<string, Task>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Speech text is empty.", nameof(text));
        if (!File.Exists(samplePath)) throw new FileNotFoundException("Saved voice sample WAV was not found.", samplePath);
        if (string.IsNullOrWhiteSpace(voice.Transcript)) throw new InvalidDataException("Saved voice transcript is empty.");

        byte[] wav = await WithServiceAsync(QwenTtsServiceKind.CustomVoice, token, progress, async (slot, ct) =>
        {
            string registeredName = "resone_" + voice.Id.ToLowerInvariant();
            if (!slot.RegisteredVoiceIds.Contains(voice.Id))
            {
                if (progress is not null) await progress($"Vocals · Registering {voice.Name} with Qwen…").ConfigureAwait(false);
                byte[] reference = await File.ReadAllBytesAsync(samplePath, ct).ConfigureAwait(false);
                var register = new JsonObject
                {
                    ["name"] = registeredName,
                    ["ref_text"] = voice.Transcript,
                    ["wav_b64"] = Convert.ToBase64String(reference)
                };
                await PostForSuccessAsync(slot, "/v1/audio/voices", register, ct).ConfigureAwait(false);
                slot.RegisteredVoiceIds.Add(voice.Id);
            }

            var body = new JsonObject
            {
                ["input"] = text.Trim(),
                ["voice"] = registeredName,
                ["response_format"] = "wav"
            };
            return await PostForBytesAsync(slot, "/v1/audio/speech", body, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        await File.WriteAllBytesAsync(outputPath, wav, token).ConfigureAwait(false);
        return outputPath;
    }

    private async Task<T> WithServiceAsync<T>(QwenTtsServiceKind kind, CancellationToken token, Func<string, Task>? progress, Func<Slot, CancellationToken, Task<T>> action)
    {
        Slot slot;
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            slot = slots[kind];
            slot.ActiveRequests++;
            TouchLocked(slot);
            await EnsureStartedLockedAsync(slot, token, progress).ConfigureAwait(false);
        }
        catch
        {
            if (slots.TryGetValue(kind, out var failed) && failed.ActiveRequests > 0) failed.ActiveRequests--;
            throw;
        }
        finally { gate.Release(); }

        try
        {
            return await action(slot, token).ConfigureAwait(false);
        }
        finally
        {
            await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                slot.ActiveRequests = Math.Max(0, slot.ActiveRequests - 1);
                TouchLocked(slot);
                ScheduleIdleStopLocked(slot);
            }
            finally { gate.Release(); }
        }
    }

    private async Task EnsureStartedLockedAsync(Slot slot, CancellationToken token, Func<string, Task>? progress)
    {
        if (slot.IsRunning) return;

        if (!runtimeEnsured)
        {
            if (progress is not null) await progress("Vocals · Preparing Qwen runtime…").ConfigureAwait(false);
            await LauncherBootstrap.EnsureRuntimeComponentAsync("tts", token).ConfigureAwait(false);
            runtimeEnsured = true;
        }

        // qwentts.cpp keeps a complete model+codec context GPU resident. Do not leave the
        // other mode resident when switching. Active work is serialized by the Resone worker.
        foreach (var other in slots.Values)
        {
            if (ReferenceEquals(other, slot) || !other.IsRunning) continue;
            if (other.ActiveRequests > 0)
                throw new InvalidOperationException($"Qwen {Display(other.Kind)} is still busy; wait for it to finish before switching TTS modes.");
            await StopLockedAsync(other, "switching Qwen TTS mode").ConfigureAwait(false);
        }

        string engine = ResolveEngineDirectory();
        string server = ResolveServer(engine);
        string model = ResolvePath(slot.Kind == QwenTtsServiceKind.VoiceDesign ? settings.QwenTtsVoiceDesignTalkerPath : settings.QwenTtsTalkerPath);
        string codec = ResolvePath(settings.QwenTtsCodecPath);
        if (!File.Exists(model)) throw new FileNotFoundException($"Qwen {Display(slot.Kind)} model was not found.", model);
        if (!File.Exists(codec)) throw new FileNotFoundException("Qwen tokenizer/codec model was not found.", codec);

        int port = ReserveLoopbackPort();
        var psi = new ProcessStartInfo
        {
            FileName = server,
            WorkingDirectory = engine,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("--model"); psi.ArgumentList.Add(model);
        psi.ArgumentList.Add("--codec"); psi.ArgumentList.Add(codec);
        psi.ArgumentList.Add("--alias"); psi.ArgumentList.Add(slot.Kind == QwenTtsServiceKind.VoiceDesign ? "resone-voice-design" : "resone-custom-voice");
        psi.ArgumentList.Add("--host"); psi.ArgumentList.Add("127.0.0.1");
        psi.ArgumentList.Add("--port"); psi.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--lang"); psi.ArgumentList.Add(string.IsNullOrWhiteSpace(settings.QwenTtsLanguage) ? "English" : settings.QwenTtsLanguage.Trim());
        psi.ArgumentList.Add("--max-batch"); psi.ArgumentList.Add(Math.Clamp(settings.QwenTtsMaxBatch, 1, 32).ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--codec-chunk-dur"); psi.ArgumentList.Add(Math.Clamp(settings.QwenTtsCodecChunkSeconds, 1f, 120f).ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (!settings.QwenTtsFlashAttention) psi.ArgumentList.Add("--no-fa");
        if (settings.QwenTtsClampFp16) psi.ArgumentList.Add("--clamp-fp16");

        slot.Output.Clear();
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => Capture(slot, e.Data);
        process.ErrorDataReceived += (_, e) => Capture(slot, e.Data);
        if (progress is not null) await progress($"{Display(slot.Kind)} · Loading Qwen server…").ConfigureAwait(false);
        if (!process.Start()) throw new InvalidOperationException("Qwen TTS server process could not be started.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        slot.Process = process;
        slot.Port = port;
        slot.RegisteredVoiceIds.Clear();

        try
        {
            await WaitUntilHealthyAsync(slot, token).ConfigureAwait(false);
            TouchLocked(slot);
            if (progress is not null) await progress($"{Display(slot.Kind)} · Qwen ready.").ConfigureAwait(false);
        }
        catch
        {
            string detail = Diagnostic(slot);
            await StopLockedAsync(slot, "startup failure").ConfigureAwait(false);
            throw new InvalidOperationException($"Qwen {Display(slot.Kind)} server failed to start. {detail}".Trim());
        }
    }

    private async Task WaitUntilHealthyAsync(Slot slot, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.QwenTtsStartupTimeoutSeconds, 5, 600)));
        Exception? last = null;
        while (!timeout.IsCancellationRequested)
        {
            if (slot.Process is null || slot.Process.HasExited)
                throw new InvalidOperationException($"qwen-server exited during model load. {Diagnostic(slot)}");
            try
            {
                using var response = await http.GetAsync(BaseUrl(slot) + "/health", timeout.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) return;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
            catch (Exception e) { last = e; }
            await Task.Delay(100, timeout.Token).ConfigureAwait(false);
        }
        token.ThrowIfCancellationRequested();
        throw new TimeoutException("Timed out waiting for qwen-server health endpoint." + (last is null ? "" : " " + last.Message));
    }

    private async Task<byte[]> PostForBytesAsync(Slot slot, string route, JsonObject body, CancellationToken token)
    {
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(BaseUrl(slot) + route, content, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            throw new InvalidOperationException($"Qwen server returned {(int)response.StatusCode} {response.ReasonPhrase}: {TrimError(error)}");
        }
        return await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
    }

    private async Task PostForSuccessAsync(Slot slot, string route, JsonObject body, CancellationToken token)
    {
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(BaseUrl(slot) + route, content, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            throw new InvalidOperationException($"Qwen server returned {(int)response.StatusCode} {response.ReasonPhrase}: {TrimError(error)}");
        }
    }

    private void TouchLocked(Slot slot)
    {
        slot.LastActivityUtc = DateTimeOffset.UtcNow;
        slot.IdleStop?.Cancel();
        slot.IdleStop?.Dispose();
        slot.IdleStop = null;
    }

    private void ScheduleIdleStopLocked(Slot slot)
    {
        if (!slot.IsRunning || slot.ScopeActive || slot.ActiveRequests > 0) return;
        int delayMs = IdleDelayMs(slot.Kind);
        var cts = new CancellationTokenSource();
        slot.IdleStop?.Cancel();
        slot.IdleStop?.Dispose();
        slot.IdleStop = cts;
        DateTimeOffset activity = slot.LastActivityUtc;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, cts.Token).ConfigureAwait(false);
                await gate.WaitAsync(cts.Token).ConfigureAwait(false);
                try
                {
                    if (!cts.IsCancellationRequested && slot.IsRunning && !slot.ScopeActive && slot.ActiveRequests == 0 && slot.LastActivityUtc == activity)
                        await StopLockedAsync(slot, $"idle for {delayMs} ms").ConfigureAwait(false);
                }
                finally { gate.Release(); }
            }
            catch (OperationCanceledException) { }
            catch { /* idle cleanup is best-effort; foreground calls will report server failures */ }
        });
    }

    private async Task StopLockedAsync(Slot slot, string reason)
    {
        slot.IdleStop?.Cancel();
        slot.IdleStop?.Dispose();
        slot.IdleStop = null;
        var process = slot.Process;
        slot.Process = null;
        slot.Port = 0;
        slot.RegisteredVoiceIds.Clear();
        if (process is null) return;
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
        }
        catch { }
        finally { process.Dispose(); }
        Capture(slot, "Stopped: " + reason);
    }

    private int IdleDelayMs(QwenTtsServiceKind kind)
    {
        string key = kind == QwenTtsServiceKind.VoiceDesign ? "voice-design" : "custom-voice";
        if (settings.ServiceDelays.TryGetValue(key, out int delay)) return Math.Clamp(delay, 0, 60 * 60 * 1000);
        return 3000;
    }

    private string ResolveEngineDirectory()
    {
        string rid = Rid();
        if (!settings.QwenTtsEngineDirectories.TryGetValue(rid, out string? relative) || string.IsNullOrWhiteSpace(relative))
            throw new PlatformNotSupportedException($"No Qwen TTS engine is configured for {rid}.");
        string engine = Wds.Resone.Api.AiRuntimeRoot.ResolveEngineDirectory("tts", relative);
        if (!Directory.Exists(engine)) throw new DirectoryNotFoundException("Qwen TTS engine directory was not found: " + engine);
        return engine;
    }

    private string ResolveServer(string engine)
    {
        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(settings.QwenTtsServerName)) names.Add(settings.QwenTtsServerName.Trim());
        if (OperatingSystem.IsWindows()) { names.Add("qwen-server.exe"); names.Add("tts-server.exe"); }
        else { names.Add("qwen-server"); names.Add("tts-server"); }
        foreach (string name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string path = Path.Combine(engine, name);
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException($"Qwen TTS server executable was not found in {engine}. Expected {string.Join(" or ", names.Distinct(StringComparer.OrdinalIgnoreCase))}.");
    }

    private string ResolvePath(string configured)
    {
        if (Path.IsPathRooted(configured)) return Path.GetFullPath(configured);

        // Prefer the manager's explicit AI root. If AI_ROOT was recently changed,
        // however, keep source-tree/existing-runtime compatibility so a model that
        // was working before does not suddenly become "missing" merely because the
        // selected future model location is empty. AiRuntimeRoot.ResolveAsset also
        // falls back to the Resone application/source root for development installs.
        string primary = Path.GetFullPath(Path.Combine(aiRoot, configured));
        if (File.Exists(primary) || Directory.Exists(primary)) return primary;
        return Path.GetFullPath(Wds.Resone.Api.AiRuntimeRoot.ResolveAsset(configured));
    }

    private static string Rid()
    {
        string os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsMacOS() ? "osx" : throw new PlatformNotSupportedException();
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException("Qwen TTS does not support this process architecture.")
        };
        return os + "-" + arch;
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private static string BaseUrl(Slot slot) => $"http://127.0.0.1:{slot.Port}";
    private static string Display(QwenTtsServiceKind kind) => kind == QwenTtsServiceKind.VoiceDesign ? "Voice Designer" : "Custom Voice";

    private static void Capture(Slot slot, string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        slot.Output.Enqueue(line.Trim());
        while (slot.Output.Count > 30) slot.Output.TryDequeue(out _);
    }

    private static string Diagnostic(Slot slot)
    {
        string output = string.Join(" | ", slot.Output.ToArray().TakeLast(8));
        if (string.IsNullOrWhiteSpace(output)) output = "No server output was captured.";
        if (slot.Process is { HasExited: true } p) output = $"Exit code {p.ExitCode}. " + output;
        return output;
    }

    private static string TrimError(string value)
    {
        value = (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length <= 1200 ? value : value[..1200] + "…";
    }

    private void ThrowIfDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(QwenTtsServiceManager));
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed) return;
            disposed = true;
            foreach (var slot in slots.Values) await StopLockedAsync(slot, "Resone shutdown").ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
            gate.Dispose();
        }
    }
}
