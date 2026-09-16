using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Wds.Resone.Api;
using Wds.Resone.Launcher;

namespace Wds.Resone.Launcher;
internal static class WorkerHost { public static async Task RunAsync(string[] args) {
string root = Environment.GetEnvironmentVariable("RESONE_HOME") ?? AppContext.BaseDirectory;
string config = Path.Combine(root, "config", "appsettings.json");
var settings = File.Exists(config) ? JsonSerializer.Deserialize(File.ReadAllText(config), ResoneJson.Default.ResoneSettings) ?? new() : new ResoneSettings();
#if RESONE_CUSTOMER_RELEASE
settings.NativeInference=true;
settings.GpuLayers=Environment.GetEnvironmentVariable("RESONE_SELECTED_BACKEND")=="cpu"?0:99;
settings.LogLlmRequests=false;
#endif
string logDiagnostics;
try { logDiagnostics = Wds.Resone.Api.Ai.LlmRequestLog.Probe(settings, Path.GetFullPath(config)); }
catch (Exception e) { logDiagnostics = "LLM log write test FAILED. Config: " + config + ". " + e.Message; }
Console.WriteLine(logDiagnostics);
var trace = new HostTrace(settings);
trace.Write("startup", detail: $"Process={Environment.ProcessPath}; Config={Path.GetFullPath(config)}; {logDiagnostics}");
using var http = new HttpClient
{
    Timeout = Timeout.InfiniteTimeSpan
};
var licensing = new LicenseService(root,http);
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
    CancellationTokenSource? jobStop = null;
    Task job = Task.CompletedTask;
    try
    {
        await Send("ready", "", new JsonObject { ["message"] = "Resone Forge ready", ["logging"] = logDiagnostics + $". Serving PID: {Environment.ProcessId}; executable: {Environment.ProcessPath}" });
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
                            await Send("status", request.RequestId, new JsonObject { ["message"] = "Composing…" });
                            trace.Write("compose-start", request.RequestId);
                            var result = await new ForgeEngine(settings, Path.Combine(root, "assets"), http).ComposeAsync(request.Payload, token);
                            token.ThrowIfCancellationRequested();
                            await Send("composition", request.RequestId, result);
                            break;
                        case "transcribe":
                            await licensing.EnsureAsync(token);
                            string text = await new SpeechService(http, settings).TranscribeAsync(Convert.FromBase64String(request.Payload.GetProperty("wav").GetString()!), token);
                            token.ThrowIfCancellationRequested();
                            await Send("transcript", request.RequestId, new JsonObject { ["text"] = text });
                            break;
                        case "speak":
                            await licensing.EnsureAsync(token);
                            int sequence = 0;
                            await new SpeechService(http, settings).SpeakAsync(request.Payload.GetProperty("text").GetString()!, bytes => Send("speechChunk", request.RequestId, new JsonObject { ["sequence"] = sequence++, ["sampleRate"] = 24000, ["pcm"] = Convert.ToBase64String(bytes) }), token);
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
