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
    public List<EnginePack> EnginePacks { get; set; } = [];
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
    public DateTimeOffset InstalledUtc { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RuntimeConfig))]
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
    private void Report(string message) { Console.WriteLine(message); onStatus?.Invoke(message); }
    public string SelectedBackend { get; private set; } = "cpu";

    public async Task StartAsync(CancellationToken token)
    {
        string path = Path.Combine(appRoot, "config", "runtime.json");
        if (!File.Exists(path)) return;
        var config = JsonSerializer.Deserialize(await File.ReadAllTextAsync(path, token), RuntimeJson.Default.RuntimeConfig) ?? new RuntimeConfig();
        ConfigureLogging(config.Logging, appRoot);

        var hardware = AiHardwarePlatformDetector.Detect();
        SelectedBackend = hardware.Platform;
        Report($"AI runtime: {hardware.Description}; root={aiRoot}");

        if (!config.UseExistingStack && config.ProvisionAiRuntime)
        {
            IReadOnlyList<EnginePack> packs = EnginePackInstaller.SelectPacks(config, hardware);
            foreach (EnginePack pack in packs)
                await EnginePackInstaller.InstallAsync(aiRoot, pack, http, Report, token).ConfigureAwait(false);

            foreach (ModelFile model in config.Models.Where(m => m.Enabled))
            {
                ValidateModelMetadata(model, config.RequireHashes);
                await DownloadModelAsync(model, token).ConfigureAwait(false);
            }
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
        foreach (ServiceSpec spec in config.Services.Where(s => s.Enabled && !(native && s.Name.Equals("LLM", StringComparison.OrdinalIgnoreCase))))
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


    internal static void ConfigureLogging(LoggingPolicy logging, string appRoot)
    {
        string directory = logging.Directory?.Trim() ?? "";
        if (directory.Length > 0)
        {
            directory = Environment.ExpandEnvironmentVariables(directory);
            if (!Path.IsPathRooted(directory)) directory = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wds", directory));
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

    private async Task DownloadModelAsync(ModelFile model, CancellationToken token)
    {
        string destination = EnginePackInstaller.Under(aiRoot, model.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string receiptPath = destination + ".wds-ai.json";

        if (File.Exists(destination) && new FileInfo(destination).Length > 0)
        {
            bool hasReceipt = File.Exists(receiptPath);
            bool metadataMatches = hasReceipt && await ReceiptMatchesAsync(receiptPath, model, token).ConfigureAwait(false);
            if (metadataMatches)
            {
                try
                {
                    await VerifyModelAsync(destination, model, token).ConfigureAwait(false);
                    return;
                }
                catch (InvalidDataException)
                {
                    Report("Replacing a model that failed verification: " + model.Id);
                }
            }
            else if (!hasReceipt)
            {
                // One-time migration: adopt a manually-copied model only when no managed receipt exists.
                try
                {
                    await VerifyModelAsync(destination, model, token).ConfigureAwait(false);
                    await WriteModelReceiptAsync(receiptPath, model, token).ConfigureAwait(false);
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
        await WriteModelReceiptAsync(receiptPath, model, token).ConfigureAwait(false);
        Report($"Model ready: {model.Id} ({model.Version})");
    }

    private static async Task<bool> ReceiptMatchesAsync(string receiptPath, ModelFile model, CancellationToken token)
    {
        if (!File.Exists(receiptPath)) return false;
        try
        {
            var receipt = JsonSerializer.Deserialize(await File.ReadAllTextAsync(receiptPath, token).ConfigureAwait(false), RuntimeJson.Default.ModelReceipt);
            return receipt is not null &&
                receipt.Id.Equals(model.Id, StringComparison.OrdinalIgnoreCase) &&
                receipt.Version.Equals(model.Version, StringComparison.Ordinal) &&
                receipt.Url.Equals(model.Url, StringComparison.Ordinal) &&
                receipt.Sha256.Equals(model.Sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static Task WriteModelReceiptAsync(string receiptPath, ModelFile model, CancellationToken token)
    {
        var receipt = new ModelReceipt
        {
            Id = model.Id,
            Version = model.Version,
            Url = model.Url,
            Sha256 = model.Sha256.ToUpperInvariant(),
            InstalledUtc = DateTimeOffset.UtcNow
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
