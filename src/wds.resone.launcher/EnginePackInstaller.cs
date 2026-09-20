using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Wds.Resone.Launcher;

public class EnginePack
{
    public bool Enabled { get; set; } = true;
    public string Id { get; set; } = "";
    public string Component { get; set; } = "llm";
    public string Version { get; set; } = "1";
    public string Rid { get; set; } = "win-x64";
    // Backend/platform selector. Current manifests use cuda, vulkan, or cpu.
    // nvidia/amd/intel/dynamic remain accepted for older runtime manifests.
    public string Platform { get; set; } = "";
    public string Backend { get; set; } = "dynamic";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string Directory { get; set; } = "engines/llm/llama-cpp-dynamic-win-x64";
    public List<string> RequiredFiles { get; set; } = [];
    public int StripComponents { get; set; } = -1;
    public long MaxArchiveBytes { get; set; } = 4L * 1024 * 1024 * 1024;
    public long MaxExtractedBytes { get; set; } = 8L * 1024 * 1024 * 1024;
    public List<EnginePackArchive> AdditionalArchives { get; set; } = [];

    public string EffectivePlatform => string.IsNullOrWhiteSpace(Platform) ? Backend : Platform;
}

public sealed class EnginePackArchive
{
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public int StripComponents { get; set; } = -1;
    public long MaxArchiveBytes { get; set; } = 4L * 1024 * 1024 * 1024;
    public long MaxExtractedBytes { get; set; } = 8L * 1024 * 1024 * 1024;
}


/// <summary>
/// Compact manifest asset used by the backend-matrix form of enginePacks.
/// Fields omitted here are derived from backend/component/RID so release manifests stay small.
/// </summary>
public sealed class CompactEnginePackAsset
{
    public bool Enabled { get; set; } = true;
    public string Id { get; set; } = "";
    public string Version { get; set; } = "1";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string Directory { get; set; } = "";
    public List<string> RequiredFiles { get; set; } = [];
    public int StripComponents { get; set; } = -1;
    public long MaxArchiveBytes { get; set; } = 4L * 1024 * 1024 * 1024;
    public long MaxExtractedBytes { get; set; } = 8L * 1024 * 1024 * 1024;
    public List<EnginePackArchive> AdditionalArchives { get; set; } = [];
}

public sealed class CompactEngineBackend
{
    public CompactEnginePackAsset? Llm { get; set; }
    public CompactEnginePackAsset? Tts { get; set; }
    public CompactEnginePackAsset? Asr { get; set; }
}

/// <summary>
/// One enginePacks array item can be either a legacy flat EnginePack or the compact
/// { cpu:{...}, cuda:{...}, vulkan:{...} } matrix. Legacy fields remain supported.
/// </summary>
public sealed class EnginePackManifestEntry : EnginePack
{
    public CompactEngineBackend? Cpu { get; set; }
    public CompactEngineBackend? Cuda { get; set; }
    public CompactEngineBackend? Vulkan { get; set; }

    public bool IsCompact => Cpu is not null || Cuda is not null || Vulkan is not null;
}

internal sealed class EnginePackReceipt
{
    public string Id { get; set; } = "";
    public string Component { get; set; } = "";
    public string Version { get; set; } = "";
    public string Rid { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string SourceFingerprint { get; set; } = "";
    public Dictionary<string, string> InstalledFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTimeOffset InstalledUtc { get; set; }
    public DateTimeOffset VerifiedUtc { get; set; }
}

public static class EnginePackInstaller
{
    private const string ReceiptName = ".wds-ai-engine.json";

    public static string Rid => (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" +
        RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant().Replace("arm64", "arm64").Replace("x64", "x64");

    internal static IReadOnlyList<EnginePack> SelectPacks(RuntimeConfig cfg, AiHardwareProfile hardware, string? component = null)
    {
        var compatible = ExpandManifestPacks(cfg.EnginePacks)
            .Where(p => p.Enabled && p.Rid.Equals(Rid, StringComparison.OrdinalIgnoreCase))
            .Where(p => string.IsNullOrWhiteSpace(component) || p.Component.Equals(component, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return compatible
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Component) ? p.Directory : p.Component, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                group.FirstOrDefault(p => PlatformEquals(p, hardware.Platform)) ??
                group.FirstOrDefault(p => PlatformEquals(p, "dynamic")) ??
                group.FirstOrDefault(p => PlatformEquals(p, "cpu")) ??
                throw new InvalidDataException($"No compatible {group.Key} engine pack for {Rid}/{hardware.Platform}."))
            .ToList();
    }

    internal static IReadOnlyList<EnginePack> ExpandManifestPacks(IEnumerable<EnginePackManifestEntry> entries)
    {
        var result = new List<EnginePack>();
        foreach (EnginePackManifestEntry entry in entries)
        {
            if (!entry.IsCompact)
            {
                result.Add(entry);
                continue;
            }

            AddCompactBackend(result, "cpu", entry.Cpu);
            AddCompactBackend(result, "cuda", entry.Cuda);
            AddCompactBackend(result, "vulkan", entry.Vulkan);
        }
        return result;
    }

    private static void AddCompactBackend(List<EnginePack> result, string backend, CompactEngineBackend? group)
    {
        if (group is null) return;
        AddCompactAsset(result, backend, "llm", group.Llm);
        AddCompactAsset(result, backend, "tts", group.Tts);
        AddCompactAsset(result, backend, "asr", group.Asr);
    }

    private static void AddCompactAsset(List<EnginePack> result, string backend, string component, CompactEnginePackAsset? asset)
    {
        if (asset is null || !asset.Enabled) return;
        string rid = Rid;
        string stem = component switch
        {
            "llm" => "llama-cpp",
            "tts" => "qwenttscpp",
            "asr" => "whispercpp",
            _ => component
        };
        var required = asset.RequiredFiles.Count > 0 ? asset.RequiredFiles : DefaultRequiredFiles(component);
        result.Add(new EnginePack
        {
            Enabled = true,
            Id = string.IsNullOrWhiteSpace(asset.Id) ? $"{stem}-{backend}-{rid}" : asset.Id.Trim(),
            Component = component,
            Version = string.IsNullOrWhiteSpace(asset.Version) ? "1" : asset.Version.Trim(),
            Rid = rid,
            Platform = backend,
            Backend = backend,
            Url = asset.Url.Trim(),
            Sha256 = asset.Sha256.Trim(),
            Directory = string.IsNullOrWhiteSpace(asset.Directory) ? $"engines/{component}/{stem}-{backend}-{rid}" : asset.Directory.Trim(),
            RequiredFiles = required,
            StripComponents = asset.StripComponents,
            MaxArchiveBytes = asset.MaxArchiveBytes,
            MaxExtractedBytes = asset.MaxExtractedBytes,
            AdditionalArchives = asset.AdditionalArchives
        });
    }

    private static List<string> DefaultRequiredFiles(string component)
        => component switch
        {
            "llm" when OperatingSystem.IsWindows() => ["llama.dll", "ggml.dll"],
            "asr" when OperatingSystem.IsWindows() => ["whisper-server.exe"],
            "tts" when OperatingSystem.IsWindows() => ["qwen-server.exe"],
            _ => []
        };

    private static string NormalizePlatform(string platform)
        => platform.Trim().ToLowerInvariant() switch
        {
            "nvidia" or "cuda" => "cuda",
            "amd" or "intel" or "vulkan" => "vulkan",
            "cpu" => "cpu",
            "dynamic" => "dynamic",
            var other => other
        };

    private static bool PlatformEquals(EnginePack pack, string platform)
        => NormalizePlatform(pack.EffectivePlatform).Equals(NormalizePlatform(platform), StringComparison.OrdinalIgnoreCase);

    internal static bool HasUsableActiveEngine(string aiRoot, string component, string platform)
    {
        component = component.Trim().ToLowerInvariant();
        string componentRoot;
        try { componentRoot = Under(aiRoot, "engines/" + component); }
        catch { return false; }

        string pointer = Path.Combine(componentRoot, ".active");
        if (!File.Exists(pointer)) return false;
        try
        {
            string relative = File.ReadAllText(pointer).Trim();
            if (string.IsNullOrWhiteSpace(relative)) return false;
            string target = Under(aiRoot, relative);
            if (!Directory.Exists(target) || !HasComponentEntrypoint(target, component)) return false;

            string receiptPath = Path.Combine(target, ReceiptName);
            if (!File.Exists(receiptPath)) return false;
            EnginePackReceipt? receipt = JsonSerializer.Deserialize(File.ReadAllText(receiptPath), RuntimeJson.Default.EnginePackReceipt);
            if (receipt is null || !receipt.Component.Equals(component, StringComparison.OrdinalIgnoreCase)) return false;

            string activePlatform = NormalizePlatform(receipt.Platform);
            string requestedPlatform = NormalizePlatform(platform);
            return activePlatform.Equals(requestedPlatform, StringComparison.OrdinalIgnoreCase) ||
                   activePlatform.Equals("dynamic", StringComparison.OrdinalIgnoreCase) ||
                   activePlatform.Equals("cpu", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static bool HasComponentEntrypoint(string target, string component)
        => component switch
        {
            "llm" when OperatingSystem.IsWindows() => File.Exists(Path.Combine(target, "llama.dll")) && File.Exists(Path.Combine(target, "ggml.dll")),
            "asr" when OperatingSystem.IsWindows() => File.Exists(Path.Combine(target, "whisper-server.exe")) || File.Exists(Path.Combine(target, "server.exe")),
            "tts" when OperatingSystem.IsWindows() => File.Exists(Path.Combine(target, "qwen-server.exe")) || File.Exists(Path.Combine(target, "tts-server.exe")),
            _ => Directory.Exists(target)
        };

    public static string Under(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/', '\\').Any(x => x == ".." || x.Contains(':')))
            throw new InvalidDataException("Package path must be relative and stay inside AI_ROOT.");
        string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string path = Path.GetFullPath(Path.Combine(rootFull, relative));
        if (!path.StartsWith(rootFull + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Package path escapes AI_ROOT.");
        return path;
    }

    private static void ValidateRequiredFiles(string target, EnginePack pack)
    {
        foreach (string relative in pack.RequiredFiles)
        {
            string full = Under(target, relative);
            if (!File.Exists(full))
                throw new FileNotFoundException($"Engine pack {pack.Id} is missing required runtime file: {relative}", full);
        }
    }

    public static async Task InstallAsync(string aiRoot, EnginePack pack, HttpClient http, Action<string> progress, CancellationToken token, bool forceFullVerification = false)
    {
        ValidateMetadata(pack);
        string target = Under(aiRoot, pack.Directory);
        string receiptPath = Path.Combine(target, ReceiptName);
        if (await IsCurrentAsync(target, receiptPath, pack, token, forceFullVerification, progress).ConfigureAwait(false))
        {
            await WriteActivePointerAsync(aiRoot, pack, token).ConfigureAwait(false);
            CleanupDuplicateManagedInstalls(aiRoot, pack, target, progress);
            return;
        }

        string work = Under(aiRoot, "work/engine-" + SafeId(pack.Id) + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        string archive = Path.Combine(work, "pack.zip");
        string stage = Path.Combine(work, "expanded");
        Directory.CreateDirectory(stage);

        try
        {
            progress($"Preparing {pack.Component} engine ({pack.EffectivePlatform})");
            await DownloadArchiveAsync(pack, http, archive, token).ConfigureAwait(false);
            await VerifyShaAsync(archive, pack.Sha256, "Engine pack SHA256 mismatch.", token).ConfigureAwait(false);
            await ExtractAsync(archive, stage, pack, token).ConfigureAwait(false);
            for (int i = 0; i < pack.AdditionalArchives.Count; i++)
            {
                EnginePackArchive source = pack.AdditionalArchives[i];
                string extraArchive = Path.Combine(work, $"pack-extra-{i + 1}.zip");
                progress($"Preparing {pack.Component} engine companion {i + 1}/{pack.AdditionalArchives.Count} ({pack.EffectivePlatform})");
                var overlay = new EnginePack
                {
                    Id = pack.Id + $"-extra-{i + 1}",
                    Component = pack.Component,
                    Version = pack.Version,
                    Rid = pack.Rid,
                    Platform = pack.Platform,
                    Backend = pack.Backend,
                    Url = source.Url,
                    Sha256 = source.Sha256,
                    StripComponents = source.StripComponents,
                    MaxArchiveBytes = source.MaxArchiveBytes,
                    MaxExtractedBytes = source.MaxExtractedBytes
                };
                await DownloadArchiveAsync(overlay, http, extraArchive, token).ConfigureAwait(false);
                await VerifyShaAsync(extraArchive, source.Sha256, "Engine companion archive SHA256 mismatch.", token).ConfigureAwait(false);
                await ExtractAsync(extraArchive, stage, overlay, token).ConfigureAwait(false);
            }
            ValidateRequiredFiles(stage, pack);

            var receipt = new EnginePackReceipt
            {
                Id = pack.Id,
                Component = pack.Component,
                Version = pack.Version,
                Rid = pack.Rid,
                Platform = pack.EffectivePlatform,
                Url = pack.Url,
                Sha256 = pack.Sha256.ToUpperInvariant(),
                SourceFingerprint = ComputeSourceFingerprint(pack),
                InstalledFileHashes = await HashInstalledFilesAsync(stage, token).ConfigureAwait(false),
                InstalledUtc = DateTimeOffset.UtcNow,
                VerifiedUtc = DateTimeOffset.UtcNow
            };
            await File.WriteAllTextAsync(Path.Combine(stage, ReceiptName), JsonSerializer.Serialize(receipt, RuntimeJson.Default.EnginePackReceipt), token).ConfigureAwait(false);

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            string backup = target + ".old-" + Guid.NewGuid().ToString("N");
            bool moved = false;
            try
            {
                if (Directory.Exists(target)) { Directory.Move(target, backup); moved = true; }
                Directory.Move(stage, target);
            }
            catch
            {
                if (moved && !Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target);
                throw;
            }
            if (moved) TryDeleteDirectory(backup);
            await WriteActivePointerAsync(aiRoot, pack, token).ConfigureAwait(false);
            CleanupDuplicateManagedInstalls(aiRoot, pack, target, progress);
            progress($"{pack.Component} engine ready ({pack.Version})");
        }
        finally
        {
            TryDeleteDirectory(work);
        }
    }

    private static void CleanupDuplicateManagedInstalls(string aiRoot, EnginePack pack, string canonicalTarget, Action<string> progress)
    {
        string component = string.IsNullOrWhiteSpace(pack.Component) ? "runtime" : pack.Component.Trim().ToLowerInvariant();
        string componentRoot = Under(aiRoot, "engines/" + component);
        if (!Directory.Exists(componentRoot)) return;

        string canonicalFullPath = Path.GetFullPath(canonicalTarget).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (string candidate in Directory.EnumerateDirectories(componentRoot))
        {
            string candidateFullPath = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (candidateFullPath.Equals(canonicalFullPath, StringComparison.OrdinalIgnoreCase)) continue;

            string candidateReceiptPath = Path.Combine(candidate, ReceiptName);
            if (!File.Exists(candidateReceiptPath)) continue;

            try
            {
                EnginePackReceipt? receipt = JsonSerializer.Deserialize(File.ReadAllText(candidateReceiptPath), RuntimeJson.Default.EnginePackReceipt);
                if (receipt is null) continue;
                if (!receipt.Component.Equals(pack.Component, StringComparison.OrdinalIgnoreCase)) continue;
                if (!receipt.Url.Equals(pack.Url, StringComparison.Ordinal)) continue;
                if (!receipt.Sha256.Equals(pack.Sha256, StringComparison.OrdinalIgnoreCase)) continue;

                TryDeleteDirectory(candidate);
                if (!Directory.Exists(candidate))
                    progress($"Removed duplicate managed {component} engine folder: {Path.GetFileName(candidate)}");
            }
            catch
            {
                // Never let optional duplicate cleanup block startup/provisioning.
            }
        }
    }

    private static async Task WriteActivePointerAsync(string aiRoot, EnginePack pack, CancellationToken token)
    {
        string component = string.IsNullOrWhiteSpace(pack.Component) ? "runtime" : pack.Component.Trim().ToLowerInvariant();
        string dir = Under(aiRoot, "engines/" + component);
        Directory.CreateDirectory(dir);
        string pointer = Path.Combine(dir, ".active");
        string temp = pointer + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temp, pack.Directory.Replace('\\', '/'), token).ConfigureAwait(false);
        File.Move(temp, pointer, true);
    }

    private static async Task<bool> IsCurrentAsync(string target, string receiptPath, EnginePack pack, CancellationToken token, bool forceFullVerification, Action<string> progress)
    {
        if (!Directory.Exists(target) || !File.Exists(receiptPath)) return false;
        try
        {
            var receipt = JsonSerializer.Deserialize(await File.ReadAllTextAsync(receiptPath, token).ConfigureAwait(false), RuntimeJson.Default.EnginePackReceipt);
            if (receipt is null ||
                !receipt.Id.Equals(pack.Id, StringComparison.OrdinalIgnoreCase) ||
                !receipt.Version.Equals(pack.Version, StringComparison.Ordinal) ||
                !receipt.Url.Equals(pack.Url, StringComparison.Ordinal) ||
                !receipt.Sha256.Equals(pack.Sha256, StringComparison.OrdinalIgnoreCase) ||
                !receipt.SourceFingerprint.Equals(ComputeSourceFingerprint(pack), StringComparison.OrdinalIgnoreCase) ||
                !receipt.Rid.Equals(pack.Rid, StringComparison.OrdinalIgnoreCase) ||
                !receipt.Platform.Equals(pack.EffectivePlatform, StringComparison.OrdinalIgnoreCase))
                return false;

            // Always perform the cheap structural check. Deleting the selected engine folder or
            // one of its required files must trigger provisioning even when the hash cache is valid.
            ValidateRequiredFiles(target, pack);
            if (!forceFullVerification) return true;

            progress($"Rechecking {pack.Component} engine integrity ({pack.EffectivePlatform})");
            if (receipt.InstalledFileHashes is null || receipt.InstalledFileHashes.Count == 0)
                return false; // old receipt: redownload once so we establish trusted per-file hashes

            foreach ((string relative, string expected) in receipt.InstalledFileHashes)
            {
                string full = Under(target, relative);
                if (!File.Exists(full)) return false;
                await using var input = File.OpenRead(full);
                string actual = Convert.ToHexString(await SHA256.HashDataAsync(input, token).ConfigureAwait(false));
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) return false;
            }

            receipt.VerifiedUtc = DateTimeOffset.UtcNow;
            await File.WriteAllTextAsync(receiptPath, JsonSerializer.Serialize(receipt, RuntimeJson.Default.EnginePackReceipt), token).ConfigureAwait(false);
            return true;
        }
        catch { return false; }
    }

    private static async Task<Dictionary<string, string>> HashInstalledFilesAsync(string root, CancellationToken token)
    {
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            if (Path.GetFileName(file).Equals(ReceiptName, StringComparison.OrdinalIgnoreCase)) continue;
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            await using var input = File.OpenRead(file);
            hashes[relative] = Convert.ToHexString(await SHA256.HashDataAsync(input, token).ConfigureAwait(false));
        }
        return hashes;
    }

    private static void ValidateMetadata(EnginePack pack)
    {
        if (string.IsNullOrWhiteSpace(pack.Id)) throw new InvalidDataException("Enabled engine pack requires an id.");
        if (string.IsNullOrWhiteSpace(pack.Version)) throw new InvalidDataException("Enabled engine pack requires a version.");
        if (pack.Sha256.Length != 64 || !pack.Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException($"Engine pack {pack.Id} requires a 64-character SHA256 hash.");
        if (!Uri.TryCreate(pack.Url, UriKind.Absolute, out Uri? url) || url.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException($"Engine pack {pack.Id} URL must use HTTPS.");
        if (pack.StripComponents < -1 || pack.StripComponents > 8) throw new InvalidDataException("stripComponents must be -1 (auto) or between 0 and 8.");
        foreach (EnginePackArchive source in pack.AdditionalArchives)
        {
            if (source.Sha256.Length != 64 || !source.Sha256.All(Uri.IsHexDigit))
                throw new InvalidDataException($"Engine pack {pack.Id} companion archive requires a 64-character SHA256 hash.");
            if (!Uri.TryCreate(source.Url, UriKind.Absolute, out Uri? extraUrl) || extraUrl.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException($"Engine pack {pack.Id} companion archive URL must use HTTPS.");
            if (source.StripComponents < -1 || source.StripComponents > 8)
                throw new InvalidDataException("Companion stripComponents must be -1 (auto) or between 0 and 8.");
        }
    }

    private static string ComputeSourceFingerprint(EnginePack pack)
    {
        string material = "engine-extract-auto-v1\n" + pack.Url + "\n" + pack.Sha256 + "\n" + pack.StripComponents + "\n" + string.Join("\n",
            pack.AdditionalArchives.Select(a => $"{a.Url}|{a.Sha256}|{a.StripComponents}"));
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(material);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static async Task DownloadArchiveAsync(EnginePack pack, HttpClient http, string archive, CancellationToken token)
    {
        using var response = await http.GetAsync(pack.Url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length > pack.MaxArchiveBytes)
            throw new InvalidDataException("Engine archive exceeds configured download size limit.");
        await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var output = File.Create(archive);
        byte[] buffer = new byte[128 * 1024];
        long bytes = 0;
        int n;
        while ((n = await input.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) > 0)
        {
            bytes = checked(bytes + n);
            if (bytes > pack.MaxArchiveBytes) throw new InvalidDataException("Engine archive exceeds configured download size limit.");
            await output.WriteAsync(buffer.AsMemory(0, n), token).ConfigureAwait(false);
        }
    }

    private static async Task ExtractAsync(string archive, string stage, EnginePack pack, CancellationToken token)
    {
        long total = 0;
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count > 50000) throw new InvalidDataException("Too many archive entries.");

        int stripComponents = ResolveStripComponents(zip, pack.StripComponents);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Archive symbolic links are not allowed.");
            total = checked(total + entry.Length);
            if (total > pack.MaxExtractedBytes) throw new InvalidDataException("Expanded engine pack exceeds configured size limit.");

            string[] parts = EntryParts(entry);
            if (parts.Length <= stripComponents) continue;
            string relative = string.Join('/', parts.Skip(stripComponents));
            string dest = Under(stage, relative);
            bool directory = IsDirectoryEntry(entry);
            if (directory) { Directory.CreateDirectory(dest); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            await using var input = entry.Open();
            await using var output = new FileStream(dest, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, token).ConfigureAwait(false);
            if (!OperatingSystem.IsWindows() && ((entry.ExternalAttributes >> 16) & 0x49) != 0)
                File.SetUnixFileMode(dest, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    // stripComponents=-1 means auto. Engine release ZIPs frequently wrap the payload in a
    // version/backend directory (for example Release/ or llama-.../). We want the runtime
    // DLLs/executables directly in the configured engine directory, but must preserve archives
    // that are already rooted correctly.
    private static int ResolveStripComponents(ZipArchive zip, int configured)
    {
        if (configured >= 0) return configured;

        List<string[]> files = zip.Entries
            .Where(entry => !IsDirectoryEntry(entry))
            .Select(EntryParts)
            .Where(parts => parts.Length > 0)
            .ToList();
        if (files.Count == 0) return 0;

        // A DLL at archive root is a strong signal that this is already the engine payload.
        if (files.Any(parts => parts.Length == 1 && parts[0].EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            return 0;

        // Prefer the common wrapper around executable runtime files. This also handles archives
        // with root README/license files plus a single real payload directory. If an EXE is already
        // at root, the common runtime prefix is zero and we correctly preserve the archive layout.
        List<string[]> runtimeFiles = files
            .Where(parts => parts[^1].EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                            parts[^1].EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .ToList();
        int runtimePrefix = CommonParentPrefixLength(runtimeFiles);
        if (runtimePrefix > 0) return Math.Min(runtimePrefix, 8);

        // Last fallback: if every file is enclosed by the same directory wrapper, remove the
        // common wrapper. This covers engine packs without DLL/EXE markers while avoiding sibling
        // directory flattening when the archive genuinely has multiple roots.
        int allFilesPrefix = CommonParentPrefixLength(files);
        return Math.Min(allFilesPrefix, 8);
    }

    private static int CommonParentPrefixLength(IReadOnlyList<string[]> files)
    {
        if (files.Count == 0) return 0;
        int common = files.Min(parts => Math.Max(0, parts.Length - 1));
        for (int index = 0; index < common; index++)
        {
            string expected = files[0][index];
            if (files.Any(parts => !parts[index].Equals(expected, StringComparison.OrdinalIgnoreCase)))
                return index;
        }
        return common;
    }

    private static string[] EntryParts(ZipArchiveEntry entry)
        => entry.FullName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static bool IsDirectoryEntry(ZipArchiveEntry entry)
        => entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');

    private static async Task VerifyShaAsync(string path, string sha, string message, CancellationToken token)
    {
        await using var input = File.OpenRead(path);
        string actual = Convert.ToHexString(await SHA256.HashDataAsync(input, token).ConfigureAwait(false));
        if (!actual.Equals(sha, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(message);
    }

    private static string SafeId(string value)
        => new(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
