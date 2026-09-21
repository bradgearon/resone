using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wds.Resone.Api;
using Wds.Resone.Api.Updates;

namespace Wds.Resone.Launcher;

public sealed class RuntimeConfig
{
    public int ManifestVersion { get; set; } = 1;
    public bool ProvisionAiRuntime { get; set; } = true;
    public bool RequireHashes { get; set; } = true;
    public Dictionary<string, DependencyPin> DependencyPins { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<EnginePackManifestEntry> EnginePacks { get; set; } = [];
    public List<ModelFile> Models { get; set; } = [];
    public List<ServiceSpec> Services { get; set; } = [];
    public UpdatePolicy Updates { get; set; } = new();
    public LoggingPolicy Logging { get; set; } = new();

    // Compatibility with older manifests. When true, provisioning is skipped.
    public bool UseExistingStack { get; set; }
}

public sealed class DependencyPin
{
    public string Repository { get; set; } = "";
    public string GitRef { get; set; } = "";
}

public sealed class ModelFile
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "1";
    public string Path { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public long MaxBytes { get; set; } = 16L * 1024 * 1024 * 1024;
}

public sealed class ServiceSpec
{
    public string Name { get; set; } = "";
    public string Executable { get; set; } = "";
    public List<string> Arguments { get; set; } = [];
    public bool Enabled { get; set; } = true;
}

internal sealed class ModelReceipt
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long FileLength { get; set; }
    public long LastWriteUtcTicks { get; set; }
    public DateTimeOffset InstalledUtc { get; set; }
    public DateTimeOffset VerifiedUtc { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RuntimeConfig))]
[JsonSerializable(typeof(EnginePackManifestEntry))]
[JsonSerializable(typeof(CompactEngineBackend))]
[JsonSerializable(typeof(CompactEnginePackAsset))]
[JsonSerializable(typeof(EnginePackArchive))]
[JsonSerializable(typeof(ModelReceipt))]
[JsonSerializable(typeof(EnginePackReceipt))]
internal partial class RuntimeJson : JsonSerializerContext;

/// <summary>
/// Launcher-owned AI runtime provisioner and optional service supervisor. Engines and models
/// live beneath AI_ROOT rather than beneath the Resone installation so multiple WDS products
/// can share the same verified binaries and model files.
/// </summary>
public sealed class ServiceSupervisor(string appRoot, string aiRoot, HttpClient http, Action<string>? onStatus = null) : IDisposable
{
    private readonly List<Process> _owned = [];
    private readonly SemaphoreSlim _provisionGate = new(1, 1);
    private RuntimeConfig? _config;
    private AiHardwareProfile? _hardware;
    private void Report(string message) { Console.WriteLine(message); onStatus?.Invoke(message); }
    public string SelectedBackend { get; private set; } = "cpu";

    public async Task StartAsync(CancellationToken token)
    {
        string path = Path.Combine(appRoot, "config", "runtime.json");
        if (!File.Exists(path)) return;
        _config = await LoadRuntimeConfigAsync(token).ConfigureAwait(false);
        ConfigureLogging(_config.Logging, appRoot);

        _hardware = AiHardwarePlatformDetector.Detect();
        SelectedBackend = _hardware.Platform;
        Report($"AI runtime: {_hardware.Description}; root={aiRoot}");

        if (!_config.UseExistingStack && _config.ProvisionAiRuntime)
        {
            bool forceLlmVerification = AiRuntimeIntegrity.IsLlmReverificationRequested(aiRoot);
            if (forceLlmVerification)
                Report("Previous local inference failure requested a full LLM engine/model integrity check.");

            await EnsureComponentCoreAsync("llm", token, forceLlmVerification).ConfigureAwait(false);

            // Clear only after both the selected LLM engine and enabled LLM model(s)
            // have completed provisioning/verification successfully. ASR and TTS are lazy.
            if (forceLlmVerification) AiRuntimeIntegrity.ClearLlmReverification(aiRoot);
        }
        else
        {
            Report("AI runtime provisioning is disabled; using files already present under AI_ROOT.");
        }

        string appSettingsPath = Path.Combine(appRoot, "config", "appsettings.json");
        bool native = File.Exists(appSettingsPath) &&
            (JsonSerializer.Deserialize(await File.ReadAllTextAsync(appSettingsPath, token), Wds.Resone.Api.ResoneJson.Default.ResoneSettings)?.LocalInferenceEnabled ?? false);
#if RESONE_CUSTOMER_RELEASE
        native = true;
#endif
        foreach (ServiceSpec spec in _config.Services.Where(s => s.Enabled && !(native && s.Name.Equals("LLM", StringComparison.OrdinalIgnoreCase))))
        {
            token.ThrowIfCancellationRequested();
            string executable = ResolveServicePath(spec.Executable);
            var start = new ProcessStartInfo(executable)
            {
                WorkingDirectory = aiRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string arg in spec.Arguments)
                start.ArgumentList.Add(arg.Replace("{root}", appRoot).Replace("{aiRoot}", aiRoot));
            start.Environment[Wds.Resone.Api.AiRuntimeRoot.EnvironmentVariable] = aiRoot;
            if (!File.Exists(start.FileName)) throw new FileNotFoundException("AI service executable is missing: " + start.FileName);

            var process = new Process { StartInfo = start, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine($"[{spec.Name}] {e.Data}"); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine($"[{spec.Name}] {e.Data}"); };
            process.Exited += (_, _) => Report($"[{spec.Name}] exited");
            process.Start();
            _owned.Add(process);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
    }

    public async Task EnsureComponentAsync(string component, CancellationToken token)
    {
        component = NormalizeComponent(component);

        // Refresh the manifest for every explicit ensure. This matters after an installer/update
        // replaces config/runtime.json while the singleton launcher is still alive; a stale
        // in-memory manifest must never prevent a missing model/engine from being restored.
        _config = await LoadRuntimeConfigAsync(token).ConfigureAwait(false);
        ConfigureLogging(_config.Logging, appRoot);
        _hardware ??= AiHardwarePlatformDetector.Detect();
        SelectedBackend = _hardware.Platform;

        if (_config.UseExistingStack || !_config.ProvisionAiRuntime)
        {
            Report($"{component.ToUpperInvariant()} runtime provisioning is disabled; using files already present under AI_ROOT.");
            return;
        }

        await EnsureComponentCoreAsync(component, token, forceFullVerification: false).ConfigureAwait(false);
    }

    private async Task EnsureComponentCoreAsync(string component, CancellationToken token, bool forceFullVerification)
    {
        RuntimeConfig config = _config ?? throw new InvalidOperationException("Runtime manifest has not been loaded.");
        AiHardwareProfile hardware = _hardware ?? throw new InvalidOperationException("AI hardware profile has not been detected.");
        await _provisionGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            // Models and engines are independent assets. Restore enabled models first so deleting
            // Gemma always causes an automatic startup download, and deleting Whisper/Qwen models
            // causes the same repair on the first lazy ASR/TTS use. Do not let an engine selection
            // problem mask a missing model.
            foreach (ModelFile model in config.Models.Where(m => m.Enabled && IsModelForComponent(m, component)))
            {
                ValidateModelMetadata(model, config.RequireHashes);
                await DownloadModelAsync(model, token, forceFullVerification && component.Equals("llm", StringComparison.OrdinalIgnoreCase)).ConfigureAwait(false);
            }

            IReadOnlyList<EnginePack> packs = EnginePackInstaller.SelectPacks(config, hardware, component);
            if (packs.Count == 0)
            {
                // A previously verified active engine may still be perfectly usable if an older or
                // temporarily stale manifest lacks the download record. This is especially useful
                // while upgrading an already-installed runtime: restore the model and continue with
                // the verified engine rather than making the whole AI stack unavailable.
                if (EnginePackInstaller.HasUsableActiveEngine(aiRoot, component, hardware.Platform))
                {
                    Report($"No downloadable {component.ToUpperInvariant()} pack matched backend {hardware.Platform}; using the existing verified active engine.");
                    return;
                }
                throw new InvalidOperationException($"No enabled {component.ToUpperInvariant()} engine pack is available for backend {hardware.Platform}.");
            }

            foreach (EnginePack pack in packs)
                await EnginePackInstaller.InstallAsync(
                    aiRoot, pack, http, Report, token,
                    forceFullVerification: forceFullVerification && component.Equals("llm", StringComparison.OrdinalIgnoreCase))
                    .ConfigureAwait(false);
        }
        finally
        {
            _provisionGate.Release();
        }
    }


    private async Task<RuntimeConfig> LoadRuntimeConfigAsync(CancellationToken token)
    {
        string path = Path.Combine(appRoot, "config", "runtime.json");
        if (!File.Exists(path)) throw new FileNotFoundException("AI runtime manifest was not found.", path);
        return JsonSerializer.Deserialize(await File.ReadAllTextAsync(path, token).ConfigureAwait(false), RuntimeJson.Default.RuntimeConfig)
            ?? new RuntimeConfig();
    }

    private static string NormalizeComponent(string component)
        => component.Trim().ToLowerInvariant() switch
        {
            "llm" => "llm",
            "tts" => "tts",
            "asr" or "stt" or "whisper" => "asr",
            _ => throw new ArgumentException("Unknown AI runtime component: " + component, nameof(component))
        };

    private static bool IsModelForComponent(ModelFile model, string component)
        => model.Path.Replace('\\', '/').StartsWith($"models/{component}/", StringComparison.OrdinalIgnoreCase);


    internal static void ConfigureLogging(LoggingPolicy logging, string appRoot)
    {
        string directory = logging.Directory?.Trim() ?? "";
        if (directory.Length > 0)
        {
            directory = Environment.ExpandEnvironmentVariables(directory);
            if (!Path.IsPathRooted(directory)) directory = Path.GetFullPath(Path.Combine(appRoot, directory));
            ResoneDailyLog.Configure(directory);
        }
        PruneOldLogs(logging.RetentionDays);
    }

    private static void PruneOldLogs(int retentionDays)
    {
        if (retentionDays <= 0) return;
        try
        {
            string dir = ResoneDailyLog.DirectoryPath;
            if (!Directory.Exists(dir)) return;
            DateTime cutoff = DateTime.Today.AddDays(-retentionDays);
            foreach (string file in Directory.EnumerateFiles(dir, "resone-*.log"))
                if (File.GetLastWriteTime(file) < cutoff) File.Delete(file);
        }
        catch { }
    }

    private string ResolveServicePath(string configured)
    {
        if (Path.IsPathRooted(configured)) return Path.GetFullPath(configured);
        string normalized = configured.Replace('\\', '/');
        string root = normalized.StartsWith("engines/", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? aiRoot : appRoot;
        string value = Path.GetFullPath(Path.Combine(root, configured));
        string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!value.StartsWith(rootFull + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("Runtime paths must remain beneath their configured root.");
        return value;
    }

    private static void ValidateModelMetadata(ModelFile model, bool requireHashes)
    {
        if (string.IsNullOrWhiteSpace(model.Id)) throw new InvalidDataException("Enabled model requires an id.");
        if (string.IsNullOrWhiteSpace(model.Version)) throw new InvalidDataException($"Model {model.Id} requires a version.");
        if (!Uri.TryCreate(model.Url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException($"Model {model.Id} URL must use HTTPS.");
        if (requireHashes && (model.Sha256.Length != 64 || !model.Sha256.All(Uri.IsHexDigit)))
            throw new InvalidDataException($"Model {model.Id} requires a 64-character SHA256 hash.");
    }

    private static bool IsLlmModel(ModelFile model)
        => model.Path.Replace('\\', '/').StartsWith("models/llm/", StringComparison.OrdinalIgnoreCase);

    private async Task DownloadModelAsync(ModelFile model, CancellationToken token, bool forceFullVerification = false)
    {
        string destination = EnginePackInstaller.Under(aiRoot, model.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string receiptPath = destination + ".wds-ai.json";

        if (File.Exists(destination) && new FileInfo(destination).Length > 0)
        {
            ModelReceipt? receipt = await ReadModelReceiptAsync(receiptPath, token).ConfigureAwait(false);
            bool metadataMatches = receipt is not null && ReceiptMatches(receipt, model);
            if (metadataMatches)
            {
                // Fast path: the download was SHA-verified once already and the file's cheap stamp
                // still matches that verified state. A worker/native failure sets a marker that
                // intentionally bypasses this cache and performs the full SHA pass next startup.
                if (!forceFullVerification && ModelFileStampMatches(destination, receipt!)) return;

                try
                {
                    if (forceFullVerification) Report("Rechecking LLM model integrity: " + model.Id);
                    await VerifyModelAsync(destination, model, token).ConfigureAwait(false);
                    await WriteModelReceiptAsync(receiptPath, destination, model, token, receipt!.InstalledUtc).ConfigureAwait(false);
                    return;
                }
                catch (InvalidDataException)
                {
                    Report("Replacing a model that failed verification: " + model.Id);
                }
            }
            else if (receipt is null)
            {
                // One-time migration: adopt a manually-copied model only after a full hash check.
                try
                {
                    await VerifyModelAsync(destination, model, token).ConfigureAwait(false);
                    await WriteModelReceiptAsync(receiptPath, destination, model, token).ConfigureAwait(false);
                    return;
                }
                catch (InvalidDataException)
                {
                    Report("Replacing an unmanaged model that failed verification: " + model.Id);
                }
            }
            else
            {
                // A managed receipt exists but its version/URL/hash changed. Redownload intentionally,
                // even if the old file happens to have the same bytes.
                Report("Updating model: " + model.Id + " -> " + model.Version);
            }
        }

        string partial = destination + ".partial";
        string partialMeta = partial + ".json";
        string expectedKey = $"{model.Id}\n{model.Version}\n{model.Url}\n{model.Sha256}";
        if (!File.Exists(partialMeta) || !string.Equals(await File.ReadAllTextAsync(partialMeta, token).ConfigureAwait(false), expectedKey, StringComparison.Ordinal))
        {
            TryDelete(partial);
            await File.WriteAllTextAsync(partialMeta, expectedKey, token).ConfigureAwait(false);
        }

        long offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;
        using var request = new HttpRequestMessage(HttpMethod.Get, model.Url);
        if (offset > 0) request.Headers.Range = new RangeHeaderValue(offset, null);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        bool resume = offset > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (resume && response.Content.Headers.ContentRange?.From != offset) throw new InvalidDataException("Model range mismatch.");
        if (!resume) offset = 0;

        Report($"Downloading model {model.Id} ({model.Version})");
        await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
        await using (var output = new FileStream(partial, resume ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None))
        {
            byte[] buffer = new byte[256 * 1024];
            long total = offset;
            int n;
            while ((n = await input.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) > 0)
            {
                total = checked(total + n);
                if (total > model.MaxBytes) throw new InvalidDataException($"Model {model.Id} exceeds configured size limit.");
                await output.WriteAsync(buffer.AsMemory(0, n), token).ConfigureAwait(false);
            }
        }
        if (new FileInfo(partial).Length == 0) throw new InvalidDataException("Empty model download.");

        try { await VerifyModelAsync(partial, model, token).ConfigureAwait(false); }
        catch { TryDelete(partial); TryDelete(partialMeta); throw; }

        File.Move(partial, destination, true);
        TryDelete(partialMeta);
        await WriteModelReceiptAsync(receiptPath, destination, model, token).ConfigureAwait(false);
        Report($"Model ready: {model.Id} ({model.Version})");
    }

    private static async Task<ModelReceipt?> ReadModelReceiptAsync(string receiptPath, CancellationToken token)
    {
        if (!File.Exists(receiptPath)) return null;
        try
        {
            return JsonSerializer.Deserialize(await File.ReadAllTextAsync(receiptPath, token).ConfigureAwait(false), RuntimeJson.Default.ModelReceipt);
        }
        catch { return null; }
    }

    private static bool ReceiptMatches(ModelReceipt receipt, ModelFile model)
        => receipt.Id.Equals(model.Id, StringComparison.OrdinalIgnoreCase) &&
           receipt.Version.Equals(model.Version, StringComparison.Ordinal) &&
           receipt.Url.Equals(model.Url, StringComparison.Ordinal) &&
           receipt.Sha256.Equals(model.Sha256, StringComparison.OrdinalIgnoreCase);

    private static bool ModelFileStampMatches(string path, ModelReceipt receipt)
    {
        if (receipt.FileLength <= 0 || receipt.LastWriteUtcTicks <= 0) return false;
        var file = new FileInfo(path);
        return file.Exists && file.Length == receipt.FileLength && file.LastWriteTimeUtc.Ticks == receipt.LastWriteUtcTicks;
    }

    private static Task WriteModelReceiptAsync(string receiptPath, string modelPath, ModelFile model, CancellationToken token, DateTimeOffset? installedUtc = null)
    {
        var file = new FileInfo(modelPath);
        var receipt = new ModelReceipt
        {
            Id = model.Id,
            Version = model.Version,
            Url = model.Url,
            Sha256 = model.Sha256.ToUpperInvariant(),
            FileLength = file.Length,
            LastWriteUtcTicks = file.LastWriteTimeUtc.Ticks,
            InstalledUtc = installedUtc ?? DateTimeOffset.UtcNow,
            VerifiedUtc = DateTimeOffset.UtcNow
        };
        return File.WriteAllTextAsync(receiptPath, JsonSerializer.Serialize(receipt, RuntimeJson.Default.ModelReceipt), token);
    }

    private static async Task VerifyModelAsync(string path, ModelFile model, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(model.Sha256)) return;
        await using var f = File.OpenRead(path);
        string actual = Convert.ToHexString(await SHA256.HashDataAsync(f, token).ConfigureAwait(false));
        if (!actual.Equals(model.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Model SHA256 mismatch: " + model.Id);
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }

    public void Dispose()
    {
        foreach (Process p in _owned)
        {
            try { if (!p.HasExited) p.Kill(true); } catch { }
            p.Dispose();
        }
    }
}
