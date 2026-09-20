using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using Wds.Resone.Api.Ai;

namespace Wds.Resone.Api;

/// <summary>
/// Lazily owns Resone's bundled whisper.cpp server process. This deliberately is not a
/// separately-installed or pre-running service: recording starts first, then WarmAsync starts
/// the engine/model in the background so it is normally ready by the time the user presses
/// Send voice. The process is private to the Resone worker and is terminated with it.
/// </summary>
internal sealed class WhisperCppRuntime(HttpClient http, ResoneSettings settings) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Process? process;
    private Uri? inferenceUri;
    private string? description;
    private bool disposed;
    private bool runtimeEnsured;

    public bool EnabledForThisPlatform
        => settings.UseLocalWhisper && OperatingSystem.IsWindows() &&
           settings.WhisperEngineDirectories.ContainsKey(LlamaEngineResolver.Rid);

    public async Task<string> WarmAsync(CancellationToken token)
    {
        ThrowIfDisposed();
        if (!EnabledForThisPlatform)
            return "Whisper HTTP compatibility mode";

        if (!runtimeEnsured)
        {
            await LauncherBootstrap.EnsureRuntimeComponentAsync("asr", token).ConfigureAwait(false);
            runtimeEnsured = true;
        }

        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (process is { HasExited: false } && inferenceUri is not null)
                return description ?? "whisper.cpp ready";

            CleanupProcess();
            string rid = LlamaEngineResolver.Rid;
            if (!settings.WhisperEngineDirectories.TryGetValue(rid, out string? configured) || string.IsNullOrWhiteSpace(configured))
                throw new PlatformNotSupportedException($"No whisper.cpp engine directory is configured for {rid}.");

            string engine = AiRuntimeRoot.ResolveEngineDirectory("asr", configured);
            if (!Directory.Exists(engine))
                throw new DirectoryNotFoundException("Whisper.cpp engine directory was not found: " + engine);

            string executable = ResolveServerExecutable(engine);
            string model = LlamaEngineResolver.ResolveAiAsset(settings.WhisperModelPath);
            if (!File.Exists(model))
                throw new FileNotFoundException("Whisper.cpp model was not found.", model);

            int port = ReserveLoopbackPort();
            var start = new ProcessStartInfo(executable)
            {
                WorkingDirectory = engine,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("-m"); start.ArgumentList.Add(model);
            start.ArgumentList.Add("--host"); start.ArgumentList.Add("127.0.0.1");
            start.ArgumentList.Add("--port"); start.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(settings.WhisperLanguage))
            {
                start.ArgumentList.Add("--language");
                start.ArgumentList.Add(settings.WhisperLanguage.Trim());
            }

            var candidate = new Process { StartInfo = start, EnableRaisingEvents = true };
            candidate.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Log("stdout: " + e.Data); };
            candidate.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Log("stderr: " + e.Data); };
            if (!candidate.Start())
                throw new InvalidOperationException("Could not start the bundled whisper.cpp runtime.");
            candidate.BeginOutputReadLine();
            candidate.BeginErrorReadLine();
            process = candidate;
            inferenceUri = new Uri($"http://127.0.0.1:{port}/inference");

            // whisper-server opens its listener only after the model/runtime is far enough along
            // to serve requests. Poll asynchronously; never block the WebSocket/UI receive path.
            var rootUri = new Uri($"http://127.0.0.1:{port}/");
            var started = Stopwatch.StartNew();
            Exception? last = null;
            while (started.Elapsed < TimeSpan.FromSeconds(Math.Max(15, Math.Min(settings.TimeoutSeconds, 120))))
            {
                token.ThrowIfCancellationRequested();
                if (candidate.HasExited)
                    throw new InvalidOperationException($"Bundled whisper.cpp exited while loading (code {candidate.ExitCode}). See the daily Resone log: {ResoneDailyLog.CurrentPath}.");
                try
                {
                    using var pingStop = CancellationTokenSource.CreateLinkedTokenSource(token);
                    pingStop.CancelAfter(TimeSpan.FromMilliseconds(500));
                    using var response = await http.GetAsync(rootUri, pingStop.Token).ConfigureAwait(false);
                    if ((int)response.StatusCode < 500)
                    {
                        description = $"whisper.cpp ready; engine={engine}; model={model}; pid={candidate.Id}";
                        Log(description);
                        return description;
                    }
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                catch (HttpRequestException e) { last = e; }
                await Task.Delay(100, token).ConfigureAwait(false);
            }

            throw new TimeoutException("Bundled whisper.cpp did not become ready in time." + (last is null ? "" : " " + last.Message));
        }
        catch
        {
            CleanupProcess();
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<string> TranscribeAsync(byte[] wav, CancellationToken token)
    {
        if (wav.Length < 44 || wav.Length > 12_000_000)
            throw new ArgumentException("Voice recording must be a WAV of at most 90 seconds.");

        if (!EnabledForThisPlatform)
            return await TranscribeHttpAsync(new Uri(settings.SttUrl), wav, token).ConfigureAwait(false);

        await WarmAsync(token).ConfigureAwait(false);
        Uri uri = inferenceUri ?? throw new InvalidOperationException("Whisper.cpp did not expose its transcription endpoint.");
        try
        {
            return await TranscribeHttpAsync(uri, wav, token).ConfigureAwait(false);
        }
        catch (HttpRequestException) when (process is null || process.HasExited)
        {
            // A driver/runtime crash should be recoverable on the next attempt rather than leaving
            // a dead cached process for the entire app session.
            await gate.WaitAsync(token).ConfigureAwait(false);
            try { CleanupProcess(); }
            finally { gate.Release(); }
            await WarmAsync(token).ConfigureAwait(false);
            return await TranscribeHttpAsync(inferenceUri!, wav, token).ConfigureAwait(false);
        }
    }

    private async Task<string> TranscribeHttpAsync(Uri uri, byte[] wav, CancellationToken token)
    {
        using var form = new MultipartFormDataContent();
        using var audio = new ByteArrayContent(wav);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audio, "file", "recording.wav");
        form.Add(new StringContent("json"), "response_format");
        using var response = await http.PostAsync(uri, form, token).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Whisper transcription failed ({(int)response.StatusCode} {response.StatusCode}): {Trim(body, 500)}");
        using var json = JsonDocument.Parse(body);
        return json.RootElement.TryGetProperty("text", out var text) ? text.GetString()?.Trim() ?? "" : "";
    }

    private string ResolveServerExecutable(string engine)
    {
        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(settings.WhisperServerName)) names.Add(settings.WhisperServerName.Trim());
        names.AddRange(OperatingSystem.IsWindows() ? ["whisper-server.exe", "server.exe"] : ["whisper-server", "server"]);
        string? full = names.Select(n => Path.Combine(engine, n)).FirstOrDefault(File.Exists);
        if (full is null)
            throw new FileNotFoundException($"Bundled whisper.cpp server executable was not found in {engine}. Expected whisper-server.exe.");
        return full;
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private void CleanupProcess()
    {
        var old = process;
        process = null;
        inferenceUri = null;
        description = null;
        if (old is null) return;
        try { if (!old.HasExited) old.Kill(entireProcessTree: true); } catch { }
        try { old.Dispose(); } catch { }
    }

    private static string Trim(string text, int max) => text.Length <= max ? text : text[..max] + "…";
    private static void Log(string message) => ResoneDailyLog.Write("WHISPER", message);
    private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(WhisperCppRuntime)); }
    public ValueTask DisposeAsync() { if (!disposed) { disposed = true; CleanupProcess(); gate.Dispose(); } return ValueTask.CompletedTask; }
}
