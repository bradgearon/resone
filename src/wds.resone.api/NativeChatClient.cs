using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
namespace Wds.Resone.Api.Ai;

// One warm model per worker. All calls are serialized off the UI/audio threads.
public sealed class NativeChatClient(ResoneSettings settings) : ILocalChatModelClient
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static nint model, library;
    private static string? identity;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Abi();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path,int context,int gpu,int threads,byte[] error,int size);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Emit(nint user,nint text,int length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Cancel(nint user);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Generate(nint handle,[MarshalAs(UnmanagedType.LPUTF8Str)] string messages,int maxTokens,Emit emit,Cancel cancel,nint user,byte[] error,int size);
    private static T Export<T>(string name) where T:Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library,name));
    public async Task<string> CompleteTextStreamingAsync(IReadOnlyList<ChatMessage> messages,int? maxTokens,string label,Action<string>? delta,CancellationToken token)
    {
        var wire=new JsonArray();foreach(var m in messages)wire.Add((JsonNode)new JsonObject{["role"]=m.Role,["content"]=m.Content});
        using var log=new LlmRequestLog(settings,label,new JsonObject{["transport"]="native",["messages"]=wire,["max_tokens"]=maxTokens??8192});
        await Gate.WaitAsync(token);
        try { var result=await Task.Run(() => Run(messages,maxTokens??8192,delta,token),token);log.Complete(result);return result; }
        catch(Exception e){log.Fail(e,"");throw;}
        finally { Gate.Release(); }
    }
    private string Run(IReadOnlyList<ChatMessage> messages,int budget,Action<string>? delta,CancellationToken token)
    {
        var root=Environment.GetEnvironmentVariable("RESONE_HOME")??AppContext.BaseDirectory;
        var id=Path.GetFullPath(Path.Combine(root,settings.NativeModelPath));
        var error=new byte[2048];
        if(model==0)
        {
            if(library==0)library=NativeLibrary.Load(Path.GetFullPath(Path.Combine(root,settings.NativeLibraryPath)));
            if(Export<Abi>("resone_inference_abi")()!=1) throw new InvalidDataException("Incompatible native inference ABI.");
            var open=Export<Open>("resone_model_open");
            model=open(id,settings.ContextTokens,settings.GpuLayers,Math.Max(1,Environment.ProcessorCount/2),error,error.Length);
            if(model==0 && settings.GpuLayers!=0 && settings.AllowCpuFallback) model=open(id,settings.ContextTokens,0,Math.Max(1,Environment.ProcessorCount/2),error,error.Length);
            if(model==0) throw new InvalidOperationException(Error(error));
            identity=id;
        }
        if(identity!=id) throw new InvalidOperationException("Restart the AI stack after changing the model.");
        var wire=new JsonArray();foreach(var m in messages)wire.Add((JsonNode)new JsonObject{["role"]=m.Role,["content"]=m.Content});
        var result=new StringBuilder();var decoder=Encoding.UTF8.GetDecoder();Exception? callbackError=null;
        Emit emit=(_,ptr,count)=>{
            try{if(token.IsCancellationRequested)return 1;var bytes=new byte[count];Marshal.Copy(ptr,bytes,0,count);var chars=new char[Encoding.UTF8.GetMaxCharCount(count)];int length=decoder.GetChars(bytes,chars,false);var text=new string(chars,0,length);result.Append(text);if(result.Length>65536)throw new InvalidDataException("Model output too long.");delta?.Invoke(text);return 0;}
            catch(Exception e){callbackError=e;return 1;}
        };
        Cancel cancel=_=>token.IsCancellationRequested?1:0;
        int status=Export<Generate>("resone_model_generate")(model,wire.ToJsonString(),budget,emit,cancel,0,error,error.Length);
        GC.KeepAlive(emit);GC.KeepAlive(cancel);
        token.ThrowIfCancellationRequested();if(callbackError!=null)throw callbackError;
        if(status!=0)throw new InvalidOperationException(Error(error));return result.ToString();
    }
    private static string Error(byte[] b)=>Encoding.UTF8.GetString(b,0,Array.IndexOf(b,(byte)0) is var i&&i>=0?i:b.Length);
}
