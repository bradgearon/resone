using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;

namespace Wds.Resone.Api.Ai;

/// <summary>
/// In-process llama.cpp client. A tiny Resone bridge dynamically loads the platform-selected
/// llama.cpp engine pack; the GGUF model/context stay resident for the lifetime of the worker.
/// All generation is serialized away from UI/audio threads and streamed token-by-token.
/// </summary>
public sealed class NativeChatClient(ResoneSettings settings) : ILocalChatModelClient
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly object LoadLock = new();
    private static nint model;
    private static nint bridge;
    private static string? identity;
    private static string? loadedDescription;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Abi();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint BridgeBuildId();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint Open(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string engineDirectory,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string modelPath,
        int contextTokens,
        int gpuLayers,
        int threads,
        int flashAttention,
        byte[] error,
        int errorSize);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Emit(nint user, nint text, int length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Cancel(nint user);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Generate(
        nint handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string messages,
        float temperature,
        int topK,
        float topP,
        Emit emit,
        Cancel cancel,
        nint user,
        byte[] error,
        int errorSize);

    private static T Export<T>(string name) where T : Delegate
        => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(bridge, name));

    public static async Task<string> WarmupAsync(ResoneSettings settings, CancellationToken token, Action<string>? diagnostic = null)
    {
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => EnsureLoaded(settings, diagnostic), token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            MarkRuntimeFailure(e);
            throw;
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<string> CompleteTextStreamingAsync(
        IReadOnlyList<ChatMessage> messages,
        string label,
        Action<string>? delta,
        CancellationToken token)
    {
        var wire = new JsonArray();
        foreach (var m in messages)
            wire.Add((JsonNode)new JsonObject { ["role"] = m.Role, ["content"] = m.Content });

        using var log = new LlmRequestLog(settings, label, new JsonObject
        {
            ["transport"] = "llama.cpp-in-process",
            ["messages"] = wire,
            ["output_limit"] = "none (EOS/context-window only)",
            ["temperature"] = settings.Temperature,
            ["top_k"] = settings.TopK,
            ["top_p"] = settings.TopP,
            ["context_tokens"] = settings.ContextTokens,
            ["flash_attention"] = settings.FlashAttention,
            ["enable_thinking"] = false,
            ["stream"] = true
        }, "llama.cpp-in-process");

        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            string result = await Task.Run(() => Run(wire.ToJsonString(), delta, token), token).ConfigureAwait(false);
            log.Complete(result);
            return result;
        }
        catch (Exception e)
        {
            log.Fail(e, "");
            if (e is not OperationCanceledException) MarkRuntimeFailure(e);
            throw;
        }
        finally
        {
            Gate.Release();
        }
    }

    private string Run(string messages, Action<string>? delta, CancellationToken token)
    {
        EnsureLoaded(settings);
        var error = new byte[4096];
        var result = new StringBuilder();
        var decoder = Encoding.UTF8.GetDecoder();
        var visible = new VisibleOutputFilter(text =>
        {
            result.Append(text);
            delta?.Invoke(text);
        });
        Exception? callbackError = null;

        Emit emit = (_, ptr, count) =>
        {
            try
            {
                if (token.IsCancellationRequested)
                    return 1;
                var bytes = new byte[count];
                Marshal.Copy(ptr, bytes, 0, count);
                var chars = new char[Encoding.UTF8.GetMaxCharCount(count)];
                int length = decoder.GetChars(bytes, chars, flush: false);
                if (length > 0)
                    visible.Push(new string(chars, 0, length));
                return 0;
            }
            catch (Exception e)
            {
                callbackError = e;
                return 1;
            }
        };
        Cancel cancel = _ => token.IsCancellationRequested ? 1 : 0;

        int status = Export<Generate>("resone_llama_generate")(
            model,
            messages,
            settings.Temperature,
            settings.TopK,
            settings.TopP,
            emit,
            cancel,
            0,
            error,
            error.Length);
        GC.KeepAlive(emit);
        GC.KeepAlive(cancel);

        token.ThrowIfCancellationRequested();
        if (callbackError is not null)
            throw callbackError;
        if (status != 0)
            throw new InvalidOperationException(Error(error));
        visible.Complete();
        return result.ToString();
    }

    // Gemma 4 thinking is disabled by the chat template, but keep reasoning channels
    // out of Resone even if a model/template variant emits one defensively. Buffer
    // only the beginning of the response until we can prove it is normal output.
    private sealed class VisibleOutputFilter(Action<string> emit)
    {
        private const string Start = "<|channel>thought";
        private const string End = "<channel|>";
        private readonly StringBuilder pending = new();
        private int state; // 0 = deciding, 1 = suppressing thought, 2 = visible output

        public void Push(string text)
        {
            if (state == 2)
            {
                emit(text);
                return;
            }

            pending.Append(text);
            if (pending.Length > 65536)
                throw new InvalidDataException("Model reasoning channel exceeded Resone's safety limit.");
            Process();
        }

        public void Complete()
        {
            if (state == 0 && pending.Length > 0)
            {
                state = 2;
                emit(pending.ToString());
                pending.Clear();
            }
            // If generation ended while a thought channel was still open, discard it.
            // It is never valid Resonator/composer output and must not enter logs/history.
        }

        private void Process()
        {
            if (state == 0)
            {
                string value = pending.ToString();
                int first = 0;
                while (first < value.Length && char.IsWhiteSpace(value[first])) first++;
                string candidate = value[first..];
                if (candidate.Length < Start.Length && Start.StartsWith(candidate, StringComparison.Ordinal))
                    return;
                if (candidate.StartsWith(Start, StringComparison.Ordinal))
                {
                    state = 1;
                    pending.Remove(0, first + Start.Length);
                }
                else
                {
                    state = 2;
                    emit(value);
                    pending.Clear();
                    return;
                }
            }

            if (state == 1)
            {
                string value = pending.ToString();
                int end = value.IndexOf(End, StringComparison.Ordinal);
                if (end < 0) return;
                string remainder = value[(end + End.Length)..];
                int firstVisible = 0;
                while (firstVisible < remainder.Length && (remainder[firstVisible] == '\r' || remainder[firstVisible] == '\n')) firstVisible++;
                pending.Clear();
                state = 2;
                if (firstVisible < remainder.Length) emit(remainder[firstVisible..]);
            }
        }
    }

    private static string EnsureLoaded(ResoneSettings settings, Action<string>? diagnostic = null)
    {
        lock (LoadLock)
        {
            diagnostic?.Invoke("Resolving llama engine directory.");
            string engine = LlamaEngineResolver.EngineDirectory(settings);
            diagnostic?.Invoke($"Engine directory: {engine}");
            string gguf = LlamaEngineResolver.Model(settings);
            diagnostic?.Invoke($"GGUF model: {gguf}; bytes={new FileInfo(gguf).Length}");
            string bridgePath = LlamaEngineResolver.Bridge(settings);
            diagnostic?.Invoke($"Resone llama bridge: {bridgePath}");
            string requestedIdentity = string.Join('|', bridgePath, engine, gguf, settings.ContextTokens,
                settings.GpuLayers, settings.FlashAttention, settings.Temperature, settings.TopK, settings.TopP);

            if (model != 0)
            {
                if (!string.Equals(identity, requestedIdentity, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Restart the Resone AI stack after changing llama engine/model/inference settings.");
                return loadedDescription ?? "llama.cpp model loaded";
            }

            if (settings.ReasoningEnabled)
                throw new InvalidOperationException("Resone local inference is intentionally configured with reasoning disabled.");

            if (bridge == 0)
            {
                diagnostic?.Invoke("Loading Resone llama bridge DLL.");
                bridge = NativeLibrary.Load(bridgePath);
                diagnostic?.Invoke("Resone llama bridge DLL loaded; checking ABI.");
                int bridgeAbi = Export<Abi>("resone_llama_bridge_abi")();
                if (bridgeAbi != 3)
                    throw new InvalidDataException($"Incompatible/stale Resone llama bridge ABI {bridgeAbi}; expected 3. Rebuild the native bridge from this source tree.");
                string bridgeBuild = Marshal.PtrToStringUTF8(Export<BridgeBuildId>("resone_llama_bridge_build_id")()) ?? "unknown";
                diagnostic?.Invoke($"Resone llama bridge ABI OK: {bridgeAbi}; build={bridgeBuild}.");
            }

            var error = new byte[4096];
            int threads = Math.Max(1, Environment.ProcessorCount / 2);
            var open = Export<Open>("resone_llama_open");
            int actualGpuLayers = settings.GpuLayers;
            diagnostic?.Invoke($"Opening llama model: context={settings.ContextTokens}; gpuLayers={actualGpuLayers}; threads={threads}; flashAttention={settings.FlashAttention}; thinking=off.");
            model = open(engine, gguf, settings.ContextTokens, actualGpuLayers, threads,
                settings.FlashAttention ? 1 : 0, error, error.Length);

            if (model == 0 && actualGpuLayers != 0 && settings.AllowCpuFallback)
            {
                string gpuError = Error(error);
                diagnostic?.Invoke("GPU model load failed: " + gpuError);
                diagnostic?.Invoke("Retrying llama model load with CPU fallback.");
                Array.Clear(error, 0, error.Length);
                actualGpuLayers = 0;
                model = open(engine, gguf, settings.ContextTokens, 0, threads,
                    settings.FlashAttention ? 1 : 0, error, error.Length);
            }
            if (model == 0)
            {
                string nativeError = Error(error);
                diagnostic?.Invoke("llama model load failed: " + nativeError);
                throw new InvalidOperationException(nativeError);
            }
            diagnostic?.Invoke($"llama model/context loaded successfully; gpuLayers={actualGpuLayers}.");

            identity = requestedIdentity;
            loadedDescription = $"llama.cpp in-process; engine={engine}; model={gguf}; context={settings.ContextTokens}; gpuLayers={actualGpuLayers}; flashAttention={settings.FlashAttention}; thinking=off; streaming=on";
            return loadedDescription;
        }
    }


    private static void MarkRuntimeFailure(Exception error)
    {
        string reason = $"{error.GetType().Name}: {error.Message}";
        Wds.Resone.Api.AiRuntimeIntegrity.RequestLlmReverification(reason);
    }

    private static string Error(byte[] bytes)
    {
        int zero = Array.IndexOf(bytes, (byte)0);
        return Encoding.UTF8.GetString(bytes, 0, zero >= 0 ? zero : bytes.Length);
    }
}
