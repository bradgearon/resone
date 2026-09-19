using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Wds.Resone.Api;
using Wds.Resone.Launcher;

namespace Wds.Resone.Launcher;
internal static class WorkerHost { public static async Task RunAsync(string[] args) {
string root = ResoneRoot.Resolve();
using var startupTrace = new StartupTrace(root);
startupTrace.Write("worker-start", $"process={Environment.ProcessPath}; cwd={Environment.CurrentDirectory}; root={root}");
string config = Path.Combine(root, "config", "appsettings.json");
startupTrace.Write("config", $"path={config}; exists={File.Exists(config)}");
ResoneSettings settings;
try {
    settings = File.Exists(config) ? JsonSerializer.Deserialize(File.ReadAllText(config), ResoneJson.Default.ResoneSettings) ?? new() : new ResoneSettings();
    startupTrace.Write("config-loaded", $"localInference={settings.LocalInferenceEnabled}; context={settings.ContextTokens}; gpuLayers={settings.GpuLayers}; flashAttention={settings.FlashAttention}; reasoning={settings.ReasoningEnabled}; warm={settings.WarmModelOnStackStart}");
}
catch (Exception e) {
    startupTrace.Write("config-failed", e.ToString());
    throw;
}
#if RESONE_CUSTOMER_RELEASE
settings.NativeInference=true;
settings.UseLocalInference=true;
settings.GpuLayers=Environment.GetEnvironmentVariable("RESONE_SELECTED_BACKEND")=="cpu"?0:99;
settings.LogLlmRequests=false;
#endif
string logDiagnostics;
try { logDiagnostics = Wds.Resone.Api.Ai.LlmRequestLog.Probe(settings, Path.GetFullPath(config)); }
catch (Exception e) { logDiagnostics = "LLM log write test FAILED. Config: " + config + ". " + e.Message; }
startupTrace.Write("request-log", logDiagnostics);
Console.WriteLine(logDiagnostics);
string inferenceDiagnostics;
if (settings.LocalInferenceEnabled)
{
    try
    {
        startupTrace.Write("inference-resolve", $"rid={Wds.Resone.Api.Ai.LlamaEngineResolver.Rid}");
        string engine = Wds.Resone.Api.Ai.LlamaEngineResolver.EngineDirectory(settings);
        string llamaLibrary = Wds.Resone.Api.Ai.LlamaEngineResolver.LlamaLibrary(settings);
        string nativeModel = Wds.Resone.Api.Ai.LlamaEngineResolver.Model(settings);
        string bridgePath = Wds.Resone.Api.Ai.LlamaEngineResolver.Bridge(settings);
        string engineFiles = string.Join(", ", Directory.EnumerateFiles(engine).Select(Path.GetFileName).OfType<string>().OrderBy(x => x));
        startupTrace.Write("inference-paths", $"engine={engine}; files=[{engineFiles}]; llama={llamaLibrary}; bridge={bridgePath}; model={nativeModel}; modelBytes={new FileInfo(nativeModel).Length}");
        inferenceDiagnostics = $"LLM transport: llama.cpp in-process; rid={Wds.Resone.Api.Ai.LlamaEngineResolver.Rid}; engine={engine}; llama={llamaLibrary}; model={nativeModel}; context={settings.ContextTokens}; gpuLayers={settings.GpuLayers}; flashAttention={settings.FlashAttention}; thinking=off; streaming=on";
        if (settings.WarmModelOnStackStart)
        {
            startupTrace.Write("inference-warmup", "begin");
            string warm = await Wds.Resone.Api.Ai.NativeChatClient.WarmupAsync(settings, CancellationToken.None, m => startupTrace.Write("inference-load", m));
            startupTrace.Write("inference-warmup", "complete: " + warm);
            inferenceDiagnostics += "; warm=" + warm;
        }
    }
    catch (Exception e)
    {
        startupTrace.Write("inference-failed", e.ToString());
        throw new InvalidOperationException("Unable to start local llama.cpp inference: " + e.Message + $". Startup log: {startupTrace.Path}", e);
    }
}
else
{
    inferenceDiagnostics = $"LLM transport: HTTP {settings.LlmUrl}";
}
startupTrace.Write("inference-ready", inferenceDiagnostics);
Console.WriteLine(inferenceDiagnostics);
var trace = new HostTrace(settings);
trace.Write("startup", detail: $"Process={Environment.ProcessPath}; Config={Path.GetFullPath(config)}; {inferenceDiagnostics}; {logDiagnostics}");
using var http = new HttpClient
{
    Timeout = Timeout.InfiniteTimeSpan
};
var licensing = new LicenseService(root,http);
var workspaceStore = new SongWorkspaceStore();
await using var ttsManager = new Wds.Resone.Api.VocalSinging.QwenTtsServiceManager(settings, root, http);
await using var speechService = new SpeechService(http, settings, ttsManager);
var voiceService = new Lazy<Wds.Resone.Api.VocalSinging.VoiceService>(() => new Wds.Resone.Api.VocalSinging.VoiceService(http, settings, speechService, ttsManager));
var builder = WebApplication.CreateSlimBuilder(args.Where(a => !a.StartsWith("--no-")).ToArray());
builder.WebHost.UseUrls("http://127.0.0.1:8078");
var app = builder.Build();
app.UseWebSockets();
app.Use(async (context,next)=>{
    var secret=Environment.GetEnvironmentVariable("RESONE_SESSION_TOKEN");
    if(context.Request.Headers.ContainsKey("Origin") || string.IsNullOrEmpty(secret) || context.Request.Headers.Authorization.ToString()!="Bearer "+secret){context.Response.StatusCode=403;return;}
    await next(context);
});
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    trace.Write("websocket-connected");
    using var stop = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
    var outgoing = Channel.CreateBounded<byte[]>(64);
    async Task Send(string op, string id, JsonNode? payload)
    {
        if (op is "error" or "cancelled" or "composition" or "ready")
            trace.Write("send-" + op, id, op == "composition" ? "Arrangement complete" : payload?.ToJsonString() ?? "");
        var e = new JsonObject
        {
            ["op"] = op,
            ["requestId"] = id,
            ["payload"] = payload
        };
        await outgoing.Writer.WriteAsync(Encoding.UTF8.GetBytes(e.ToJsonString()), stop.Token);
    }

    var writer = Task.Run(async () =>
    {
        try
        {
            await foreach (var bytes in outgoing.Reader.ReadAllAsync(stop.Token))
                await ws.SendAsync(bytes, WebSocketMessageType.Text, true, stop.Token);
        }
        catch
        {
            stop.Cancel();
            ws.Abort();
        }
    });

    // Keep local persistence/library traffic off the WebSocket receive loop. Requests are
    // queued in arrival order so Save -> Load ordering remains deterministic, but disk I/O
    // can await without preventing cancel/generation/transport messages from being read.
    var localRequests = Channel.CreateUnbounded<Envelope>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = true,
        AllowSynchronousContinuations = false
    });
    var localWorker = Task.Run(async () =>
    {
        try
        {
            await foreach (var request in localRequests.Reader.ReadAllAsync(stop.Token))
            {
                try
                {
                    switch (request.Op)
                    {
                        case "workspaceList":
                            await Send("workspaceList", request.RequestId, await workspaceStore.ListPayloadAsync(stop.Token));
                            break;
                        case "workspaceLoad":
                            await Send("workspaceLoaded", request.RequestId, await workspaceStore.LoadPayloadAsync(request.Payload.GetProperty("id").GetString() ?? "", stop.Token));
                            break;
                        case "workspaceSave":
                            await Send("workspaceSaved", request.RequestId, await workspaceStore.SaveAsync(request.Payload, stop.Token));
                            break;
                        case "workspaceDelete":
                            await Send("workspaceDeleted", request.RequestId, await workspaceStore.DeleteAsync(request.Payload.GetProperty("id").GetString() ?? "", stop.Token));
                            break;
                        case "voiceLibraryList":
                            await Send("voiceLibrary", request.RequestId, await Task.Run(() => voiceService.Value.Library(), stop.Token));
                            break;
                        case "voiceSelect":
                            await voiceService.Value.SelectVoiceAsync(request.Payload.GetProperty("id").GetString() ?? "", stop.Token);
                            await Send("voiceSelected", request.RequestId, await Task.Run(() => voiceService.Value.Library(), stop.Token));
                            break;
                        case "voiceDiscardPreview":
                            await Task.Run(() => voiceService.Value.DiscardPreview(request.Payload.GetProperty("previewId").GetString() ?? ""), stop.Token);
                            await Send("voicePreviewDiscarded", request.RequestId, new JsonObject());
                            break;
                        case "voiceDesignOpen":
                            await voiceService.Value.SetDesignerOpenAsync(true, stop.Token, message => Send("status", request.RequestId, new JsonObject { ["message"] = message }));
                            await Send("voiceDesignerReady", request.RequestId, new JsonObject());
                            break;
                        case "voiceDesignClose":
                            await voiceService.Value.SetDesignerOpenAsync(false, stop.Token);
                            await Send("voiceDesignerClosed", request.RequestId, new JsonObject());
                            break;
                        case "voiceServiceActivity":
                            string service = request.Payload.TryGetProperty("service", out var serviceValue) ? serviceValue.GetString() ?? "" : "";
                            var kind = service.Equals("voice-design", StringComparison.OrdinalIgnoreCase)
                                ? Wds.Resone.Api.VocalSinging.QwenTtsServiceKind.VoiceDesign
                                : service.Equals("custom-voice", StringComparison.OrdinalIgnoreCase)
                                    ? Wds.Resone.Api.VocalSinging.QwenTtsServiceKind.CustomVoice
                                    : throw new ArgumentException("Unknown voice service activity type: " + service);
                            await ttsManager.ActivityAsync(kind, stop.Token, message => Send("status", request.RequestId, new JsonObject { ["message"] = message }));
                            await Send("voiceServiceReady", request.RequestId, new JsonObject { ["service"] = service });
                            break;
                    }
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                catch (Exception e)
                {
                    trace.Write("local-request-failed", request.RequestId, e.ToString());
                    await Send("error", request.RequestId, new JsonObject { ["message"] = e.Message });
                }
            }
        }
        catch (OperationCanceledException) { }
    });

    CancellationTokenSource? jobStop = null;
    Task job = Task.CompletedTask;
    try
    {
        await Send("ready", "", new JsonObject { ["message"] = "Resone Forge ready", ["logging"] = inferenceDiagnostics + ". " + logDiagnostics + $". Serving PID: {Environment.ProcessId}; executable: {Environment.ProcessPath}" });
        var buffer = new byte[16384];
        while (!stop.IsCancellationRequested)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult frame;
            do
            {
                frame = await ws.ReceiveAsync(buffer, stop.Token);
                if (frame.MessageType == WebSocketMessageType.Close)
                    break;
                message.Write(buffer, 0, frame.Count);
                if (message.Length > 16 * 1024 * 1024)
                    throw new InvalidDataException("Request exceeds 16 MiB.");
            }
            while (!frame.EndOfMessage);
            if (frame.MessageType == WebSocketMessageType.Close)
                break;
            trace.Write("request-received", detail: "Command received (body omitted).");
            var request = JsonSerializer.Deserialize(message.ToArray(), ResoneJson.Default.Envelope) ?? throw new InvalidDataException("Invalid command.");
            if (request.Op == "cancel")
            {
                jobStop?.Cancel();
                continue;
            }

            // ASR warmup is deliberately independent of the foreground generation slot. The
            // microphone is already recording in the native editor while this loads whisper.cpp.
            if (request.Op == "asrWarm")
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        string detail = await speechService.WarmTranscriptionAsync(stop.Token);
                        trace.Write("asr-warm-ready", request.RequestId, detail);
                        await Send("asrReady", request.RequestId, new JsonObject { ["message"] = "Transcription engine ready." });
                    }
                    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                    catch (Exception e)
                    {
                        trace.Write("asr-warm-failed", request.RequestId, e.ToString());
                        await Send("asrWarmError", request.RequestId, new JsonObject { ["message"] = e.Message });
                    }
                });
                continue;
            }

            bool localLibraryOp = request.Op is "workspaceList" or "workspaceLoad" or "workspaceSave" or "workspaceDelete"
                or "voiceLibraryList" or "voiceSelect" or "voiceDiscardPreview" or "voiceDesignOpen" or "voiceDesignClose" or "voiceServiceActivity";
            if (localLibraryOp)
            {
                await localRequests.Writer.WriteAsync(request, stop.Token);
                continue;
            }

            if (!job.IsCompleted)
            {
                await Send("error", request.RequestId, new JsonObject { ["message"] = "A request is running. Cancel it first." });
                continue;
            }

            jobStop?.Dispose();
            jobStop = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            jobStop.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            var token = jobStop.Token;
            job = Task.Run(async () =>
            {
                try
                {
                    switch (request.Op)
                    {
                        case "licenseActivate":
                            await Send("licenseStatus",request.RequestId,await licensing.ActivateAsync(request.Payload.GetProperty("key").GetString()!,token));
                            break;
                        case "licenseRelease":
                            await Send("licenseStatus",request.RequestId,await licensing.ReleaseAsync(token));
                            break;
                        case "compose":
                            await licensing.EnsureAsync(token);
                            trace.Write("compose-start", request.RequestId);
                            var result = await new ForgeEngine(settings, Path.Combine(root, "assets"), http, ttsManager).ComposeAsync(
                                request.Payload,
                                token,
                                message => Send("status", request.RequestId, new JsonObject { ["message"] = message }));
                            token.ThrowIfCancellationRequested();
                            await Send("composition", request.RequestId, result);
                            break;
                        case "songDesign":
                            await licensing.EnsureAsync(token);
                            trace.Write("song-design-start", request.RequestId);
                            var songDesign = await new ForgeEngine(settings, Path.Combine(root, "assets"), http, ttsManager).DesignSongAsync(
                                request.Payload, token,
                                message => Send("status", request.RequestId, new JsonObject { ["message"] = message }));
                            token.ThrowIfCancellationRequested();
                            await Send("songDesign", request.RequestId, songDesign);
                            break;
                        case "songChunk":
                            await licensing.EnsureAsync(token);
                            trace.Write("song-chunk-start", request.RequestId);
                            var songChunk = await new ForgeEngine(settings, Path.Combine(root, "assets"), http, ttsManager).ComposeSongChunkAsync(
                                request.Payload, token,
                                message => Send("status", request.RequestId, new JsonObject { ["message"] = message }));
                            token.ThrowIfCancellationRequested();
                            await Send("songChunk", request.RequestId, songChunk);
                            break;
                        case "voiceDesignPreview":
                            await licensing.EnsureAsync(token);
                            trace.Write("voice-design-preview", request.RequestId);
                            var voiceDesignPreview = await voiceService.Value.DesignPreviewAsync(
                                request.Payload, token, message => Send("status", request.RequestId, new JsonObject { ["message"] = message }));
                            token.ThrowIfCancellationRequested();
                            await Send("voicePreview", request.RequestId, voiceDesignPreview);
                            break;
                        case "voiceImportPreview":
                            await licensing.EnsureAsync(token);
                            trace.Write("voice-import-preview", request.RequestId);
                            var voiceImportPreview = await voiceService.Value.ImportPreviewAsync(
                                request.Payload, token, message => Send("status", request.RequestId, new JsonObject { ["message"] = message }));
                            token.ThrowIfCancellationRequested();
                            await Send("voicePreview", request.RequestId, voiceImportPreview);
                            break;
                        case "voiceSavePreview":
                            await licensing.EnsureAsync(token);
                            trace.Write("voice-save-preview", request.RequestId);
                            var savedVoice = await voiceService.Value.SavePreviewAsync(
                                request.Payload, token, message => Send("status", request.RequestId, new JsonObject { ["message"] = message }));
                            token.ThrowIfCancellationRequested();
                            await Send("voiceSaved", request.RequestId, savedVoice);
                            break;
                        case "renderVocals":
                            await licensing.EnsureAsync(token);
                            trace.Write("vocal-render-start", request.RequestId);
                            var vocal = await new ForgeEngine(settings, Path.Combine(root, "assets"), http, ttsManager).RenderVocalsAsync(
                                request.Payload, token,
                                message => Send("status", request.RequestId, new JsonObject { ["message"] = message }));
                            token.ThrowIfCancellationRequested();
                            await Send("vocalsRendered", request.RequestId, vocal);
                            break;
                        case "transcribe":
                            await licensing.EnsureAsync(token);
                            string text = await speechService.TranscribeAsync(Convert.FromBase64String(request.Payload.GetProperty("wav").GetString()!), token);
                            token.ThrowIfCancellationRequested();
                            await Send("transcript", request.RequestId, new JsonObject { ["text"] = text });
                            break;
                        case "speak":
                            await licensing.EnsureAsync(token);
                            int sequence = 0;
                            await speechService.SpeakAsync(request.Payload.GetProperty("text").GetString()!, bytes => Send("speechChunk", request.RequestId, new JsonObject { ["sequence"] = sequence++, ["sampleRate"] = 24000, ["pcm"] = Convert.ToBase64String(bytes) }), token);
                            await Send("speechEnd", request.RequestId, new JsonObject { ["sequence"] = sequence });
                            break;
                        case "export":
                            var project = request.Payload.Deserialize(ResoneJson.Default.SongProject) ?? throw new ArgumentException("Missing project.");
                            await Send("midi", request.RequestId, new JsonObject { ["data"] = Convert.ToBase64String(ForgeEngine.Export(project)) });
                            break;
                        case "render":
                            string notation = request.Payload.GetProperty("notation").GetString()!;
                            if (notation.Length > 65536)
                                throw new ArgumentException("Notation too long.");
                            var midi = new Wds.Resone.Resonator.ResonatorMidiGenerator().Generate(notation);
                            await Send("midi", request.RequestId, new JsonObject { ["data"] = Convert.ToBase64String(midi.MidiBytes) });
                            break;
                        default:
                            throw new ArgumentException("Unknown operation: " + request.Op);
                    }
                }
                catch (OperationCanceledException)
                {
                    await Send("cancelled", request.RequestId, new JsonObject { ["message"] = "Cancelled or request timed out." });
                }
                catch (Exception e)
                {
                    trace.Write("request-failed", request.RequestId, e.ToString());
                    await Send("error", request.RequestId, new JsonObject { ["message"] = e.Message });
                }
            });
        }
    }
    catch (OperationCanceledException)
    {
    }
    catch (WebSocketException)
    {
    }
    catch (Exception e)
    {
        trace.Write("connection-failed", detail: e.ToString());
        Console.Error.WriteLine(e.Message);
    }
    finally
    {
        jobStop?.Cancel();
        stop.Cancel();
        ws.Abort();
        try
        {
            await job;
        }
        catch
        {
        }

        jobStop?.Dispose();
        localRequests.Writer.TryComplete();
        try { await localWorker; } catch { }
        outgoing.Writer.TryComplete();
        await writer;
    }
});
try
{
    await app.StartAsync();
    trace.Write("listening", detail: "http://127.0.0.1:8078");
}
catch (Exception e)
{
    trace.Write("bind-failed", detail: e.ToString());
    throw;
}
var readyName=Environment.GetEnvironmentVariable("RESONE_READY_PIPE");
if(!string.IsNullOrEmpty(readyName)) {
    using var pipe=new System.IO.Pipes.NamedPipeClientStream(".",readyName,System.IO.Pipes.PipeDirection.Out,System.IO.Pipes.PipeOptions.Asynchronous|System.IO.Pipes.PipeOptions.CurrentUserOnly);
    await pipe.ConnectAsync(10000);
    using var writer=new StreamWriter(pipe){AutoFlush=true};await writer.WriteLineAsync("ready");
}
await app.WaitForShutdownAsync();

}}
