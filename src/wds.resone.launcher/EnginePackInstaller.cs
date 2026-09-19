using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Wds.Resone.Launcher;

public sealed class EnginePack
{
    public bool Enabled { get; set; } = true;
    public string Id { get; set; } = "";
    public string Component { get; set; } = "llm";
    public string Version { get; set; } = "1";
    public string Rid { get; set; } = "win-x64";
    // Hardware family: nvidia, amd, intel, apple, cpu, or dynamic.
    // Backend remains as a compatibility alias for older runtime manifests.
    public string Platform { get; set; } = "";
    public string Backend { get; set; } = "dynamic";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string Directory { get; set; } = "engines/llm/llama-cpp-dynamic-win-x64";
    public List<string> RequiredFiles { get; set; } = [];
    public int StripComponents { get; set; }
    public long MaxArchiveBytes { get; set; } = 4L * 1024 * 1024 * 1024;
    public long MaxExtractedBytes { get; set; } = 8L * 1024 * 1024 * 1024;

    public string EffectivePlatform => string.IsNullOrWhiteSpace(Platform) ? Backend : Platform;
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
    public DateTimeOffset InstalledUtc { get; set; }
}

public static class EnginePackInstaller
{
    private const string ReceiptName = ".wds-ai-engine.json";

    public static string Rid => (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" +
        RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant().Replace("arm64", "arm64").Replace("x64", "x64");

    internal static IReadOnlyList<EnginePack> SelectPacks(RuntimeConfig cfg, AiHardwareProfile hardware)
    {
        var compatible = cfg.EnginePacks.Where(p => p.Enabled && p.Rid.Equals(Rid, StringComparison.OrdinalIgnoreCase)).ToList();
        return compatible
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Component) ? p.Directory : p.Component, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                group.FirstOrDefault(p => PlatformEquals(p, hardware.Platform)) ??
                group.FirstOrDefault(p => PlatformEquals(p, "dynamic")) ??
                group.FirstOrDefault(p => PlatformEquals(p, "cpu")) ??
                throw new InvalidDataException($"No compatible {group.Key} engine pack for {Rid}/{hardware.Platform}."))
            .ToList();
    }

    private static bool PlatformEquals(EnginePack pack, string platform)
        => pack.EffectivePlatform.Equals(platform, StringComparison.OrdinalIgnoreCase);

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

    public static async Task InstallAsync(string aiRoot, EnginePack pack, HttpClient http, Action<string> progress, CancellationToken token)
    {
        ValidateMetadata(pack);
        string target = Under(aiRoot, pack.Directory);
        string receiptPath = Path.Combine(target, ReceiptName);
        if (await IsCurrentAsync(target, receiptPath, pack, token).ConfigureAwait(false))
        {
            await WriteActivePointerAsync(aiRoot, pack, token).ConfigureAwait(false);
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
                InstalledUtc = DateTimeOffset.UtcNow
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
            progress($"{pack.Component} engine ready ({pack.Version})");
        }
        finally
        {
            TryDeleteDirectory(work);
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

    private static async Task<bool> IsCurrentAsync(string target, string receiptPath, EnginePack pack, CancellationToken token)
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
                !receipt.Rid.Equals(pack.Rid, StringComparison.OrdinalIgnoreCase) ||
                !receipt.Platform.Equals(pack.EffectivePlatform, StringComparison.OrdinalIgnoreCase))
                return false;
            ValidateRequiredFiles(target, pack);
            return true;
        }
        catch { return false; }
    }

    private static void ValidateMetadata(EnginePack pack)
    {
        if (string.IsNullOrWhiteSpace(pack.Id)) throw new InvalidDataException("Enabled engine pack requires an id.");
        if (string.IsNullOrWhiteSpace(pack.Version)) throw new InvalidDataException("Enabled engine pack requires a version.");
        if (pack.Sha256.Length != 64 || !pack.Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException($"Engine pack {pack.Id} requires a 64-character SHA256 hash.");
        if (!Uri.TryCreate(pack.Url, UriKind.Absolute, out Uri? url) || url.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException($"Engine pack {pack.Id} URL must use HTTPS.");
        if (pack.StripComponents < 0 || pack.StripComponents > 8) throw new InvalidDataException("stripComponents must be between 0 and 8.");
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
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Archive symbolic links are not allowed.");
            total = checked(total + entry.Length);
            if (total > pack.MaxExtractedBytes) throw new InvalidDataException("Expanded engine pack exceeds configured size limit.");

            string[] parts = entry.FullName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= pack.StripComponents) continue;
            string relative = string.Join('/', parts.Skip(pack.StripComponents));
            string dest = Under(stage, relative);
            bool directory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
            if (directory) { Directory.CreateDirectory(dest); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            await using var input = entry.Open();
            await using var output = new FileStream(dest, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, token).ConfigureAwait(false);
            if (!OperatingSystem.IsWindows() && ((entry.ExternalAttributes >> 16) & 0x49) != 0)
                File.SetUnixFileMode(dest, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

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
