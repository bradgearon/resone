using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Wds.Resone.Resonator;
using System.Threading.Channels;

namespace Wds.Resone.Api;
/// <summary>C ABI owns each connection until close returns. Callbacks are UTF8 borrowed for the call only.</summary>
public static unsafe class NativeExports
{
    private static readonly ConcurrentDictionary<long, BridgeClient> Clients = new();
    private static long _next;
    [ThreadStatic] private static string? _lastError;

    private static nint CopyResult(byte[] bytes, int* outputLength)
    {
        if (outputLength == null)
            return 0;
        *outputLength = bytes.Length;
        if (bytes.Length == 0)
            return 0;
        nint memory = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, memory, bytes.Length);
        return memory;
    }

    private static nint Fail(Exception ex, int* outputLength)
    {
        _lastError = ex.Message;
        if (outputLength != null)
            *outputLength = 0;
        return 0;
    }

    private static string ReadUtf8(byte* value, int length, string name)
    {
        if (value == null || length <= 0 || length > 16 * 1024 * 1024)
            throw new ArgumentException($"Invalid {name}.");
        return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(value, length));
    }

    [UnmanagedCallersOnly(EntryPoint = "resone_render_midi", CallConvs = [typeof(CallConvCdecl)])]
    public static nint RenderMidi(byte* notation, int length, int* outputLength)
    {
        try
        {
            _lastError = null;
            string text = ReadUtf8(notation, length, "notation");
            if (text.Length > 65536)
                throw new ArgumentException("Notation too long.");
            return CopyResult(new ResonatorMidiGenerator().Generate(text).MidiBytes, outputLength);
        }
        catch (Exception ex) { return Fail(ex, outputLength); }
    }

    [UnmanagedCallersOnly(EntryPoint = "resone_export_project_midi", CallConvs = [typeof(CallConvCdecl)])]
    public static nint ExportProjectMidi(byte* json, int length, int* outputLength)
    {
        try
        {
            _lastError = null;
            string text = ReadUtf8(json, length, "project JSON");
            var project = JsonSerializer.Deserialize(text, ResoneJson.Default.SongProject) ?? throw new ArgumentException("Missing project.");
            return CopyResult(ForgeEngine.Export(project), outputLength);
        }
        catch (Exception ex) { return Fail(ex, outputLength); }
    }

    [UnmanagedCallersOnly(EntryPoint = "resone_export_lane_midi", CallConvs = [typeof(CallConvCdecl)])]
    public static nint ExportLaneMidi(byte* json, int jsonLength, byte* laneId, int laneIdLength, int* outputLength)
    {
        try
        {
            _lastError = null;
            string text = ReadUtf8(json, jsonLength, "project JSON");
            string id = ReadUtf8(laneId, laneIdLength, "lane id");
            var project = JsonSerializer.Deserialize(text, ResoneJson.Default.SongProject) ?? throw new ArgumentException("Missing project.");
            return CopyResult(ForgeEngine.ExportLane(project, id), outputLength);
        }
        catch (Exception ex) { return Fail(ex, outputLength); }
    }

    [UnmanagedCallersOnly(EntryPoint = "resone_last_error", CallConvs = [typeof(CallConvCdecl)])]
    public static nint LastError(int* outputLength)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(_lastError ?? "Local Resonator operation failed.");
            return CopyResult(bytes, outputLength);
        }
        catch { if (outputLength != null) *outputLength = 0; return 0; }
    }

    [UnmanagedCallersOnly(EntryPoint = "resone_free_buffer", CallConvs = [typeof(CallConvCdecl)])]
    public static void FreeBuffer(nint buffer)
    {
        if (buffer != 0)
            Marshal.FreeHGlobal(buffer);
    }
    [UnmanagedCallersOnly(EntryPoint = "resone_set_home", CallConvs = [typeof(CallConvCdecl)])]
    public static void SetHome(byte* path) { try { LauncherBootstrap.InstallRoot=Marshal.PtrToStringUTF8((nint)path); } catch {} }
    [UnmanagedCallersOnly(EntryPoint = "resone_open", CallConvs = [typeof(CallConvCdecl)])]
    public static long Open(byte* url, delegate* unmanaged[Cdecl]<nint, byte*, int, void> callback, nint user)
    {
        try
        {
            if (callback == null)
                return 0;
            var uri = new Uri(Marshal.PtrToStringUTF8((nint)url)!);
            if (uri.Scheme != "ws" && uri.Scheme != "wss")
                return 0;
            long id = Interlocked.Increment(ref _next);
            var client = new BridgeClient(uri, (bytes) =>
            {
                fixed (byte* p = bytes)
                    callback(user, p, bytes.Length);
            });
            Clients[id] = client;
            client.Start();
            return id;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "resone_send", CallConvs = [typeof(CallConvCdecl)])]
    public static int Send(long handle, byte* utf8, int length)
    {
        try
        {
            return utf8 != null && length > 0 && length <= 16 * 1024 * 1024 && Clients.TryGetValue(handle, out var client) && client.Send(new ReadOnlySpan<byte>(utf8, length).ToArray()) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "resone_close", CallConvs = [typeof(CallConvCdecl)])]
    public static void Close(long handle)
    {
        try
        {
            if (Clients.TryRemove(handle, out var client))
                client.Dispose();
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "resone_abi_version", CallConvs = [typeof(CallConvCdecl)])]
    public static int Version() => 2;
}

internal sealed class BridgeClient(Uri uri, Action<byte[]> onEvent) : IDisposable
{
    private ClientWebSocket? socket;
    private readonly CancellationTokenSource stop = new();
    private readonly Channel<byte[]> outbox = Channel.CreateBounded<byte[]>(32);
    private Task? run;
    private volatile bool accepting=true;
    public void Start() => run = Task.Run(RunAsync);
    public bool Send(byte[] data) => accepting && outbox.Writer.TryWrite(data);
    private void Emit(string op,string message)=>onEvent(Encoding.UTF8.GetBytes(new JsonObject{["op"]=op,["requestId"]="",["payload"]=new JsonObject{["message"]=message}}.ToJsonString()));
    private async Task RunAsync()
    {
        int attempts=0;
        while(!stop.IsCancellationRequested)
        {
            using var connection=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            using var ws=new ClientWebSocket();socket=ws;Task? writer=null;
            try
            {
                if(uri.IsLoopback&&uri.Port==8078)ws.Options.SetRequestHeader("Authorization","Bearer "+await LauncherBootstrap.EnsureAsync(stop.Token));
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token)){timeout.CancelAfter(TimeSpan.FromSeconds(15));await ws.ConnectAsync(uri,timeout.Token);}
                Emit("connected","Connected");attempts=0;
                writer=Task.Run(async()=>{
                    try{await foreach(var bytes in outbox.Reader.ReadAllAsync(connection.Token))await ws.SendAsync(bytes,WebSocketMessageType.Text,true,connection.Token);}
                    catch{connection.Cancel();ws.Abort();throw;}
                });
                var chunk=new byte[16384];
                while(!connection.IsCancellationRequested)
                {
                    using var message=new MemoryStream();WebSocketReceiveResult part;
                    do{
                        part=await ws.ReceiveAsync(chunk,connection.Token);
                        if(part.MessageType==WebSocketMessageType.Close)throw new WebSocketException("Host closed the connection.");
                        if(part.MessageType!=WebSocketMessageType.Text)throw new InvalidDataException("Expected JSON event.");
                        message.Write(chunk,0,part.Count);if(message.Length>16*1024*1024)throw new InvalidDataException("Event too large.");
                    }while(!part.EndOfMessage);
                    onEvent(message.ToArray());
                }
            }
            catch(OperationCanceledException) when(stop.IsCancellationRequested){break;}
            catch(Exception e){Emit("error",e.Message);}
            finally
            {
                connection.Cancel();ws.Abort();
                if(writer!=null)try{await writer;}catch{}
                // Never replay an interrupted composition or activation request.
                while(outbox.Reader.TryRead(out _)){}
                Emit("disconnected","Connection lost. Reconnecting to the local launcher…");
            }
            if(!uri.IsLoopback||uri.Port!=8078||++attempts>3)break;
            try{await Task.Delay(TimeSpan.FromSeconds(attempts),stop.Token);}catch(OperationCanceledException){break;}
        }
        accepting=false;
    }
    public void Dispose(){accepting=false;stop.Cancel();socket?.Abort();outbox.Writer.TryComplete();run?.GetAwaiter().GetResult();stop.Dispose();}
}
