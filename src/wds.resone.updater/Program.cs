using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Wds.Resone.Api;
using Wds.Resone.Api.Updates;
using Wds.Resone.Updater;

static string? Arg(string[] args, string name)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return i + 1 < args.Length ? args[i + 1] : null;
        string prefix = name + "=";
        if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return args[i][prefix.Length..];
    }
    return null;
}

string installRoot = Path.GetFullPath(Arg(args, "--install-root") ?? throw new ArgumentException("--install-root is required."));
string targetVersion = Arg(args, "--target-version") ?? "";
string? aiRoot = Arg(args, "--ai-root");
int parentPid = int.TryParse(Arg(args, "--parent-pid"), out int parsedPid) ? parsedPid : 0;
RuntimeUpdateEnvelope envelope = await LoadLocalConfigAsync(installRoot, CancellationToken.None);
UpdatePolicy policy = envelope.Updates;
ConfigureLogging(envelope.Logging, installRoot);
ResoneDailyLog.Write("UPDATER", $"Updater started. target={targetVersion}; installRoot={installRoot}");

string? backupRoot = null;
bool appSwapped = false;
try
{
    await WaitForParentAsync(parentPid, policy.ParentExitTimeoutSeconds);
    using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    UpdateManifest manifest = await FetchManifestAsync(http, policy, CancellationToken.None);
    if (!manifest.Product.Equals("resone", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update manifest product is not Resone.");
    if (!string.IsNullOrWhiteSpace(targetVersion) && UpdateVersion.Compare(manifest.Version, targetVersion) < 0)
        throw new InvalidDataException($"Update manifest moved backwards from requested {targetVersion} to {manifest.Version}.");

    string rid = CurrentRid();
    UpdateArtifact app = manifest.Artifacts.FirstOrDefault(a => a.Enabled && a.Kind.Equals("app", StringComparison.OrdinalIgnoreCase) && RidMatches(a.Rid, rid))
        ?? throw new InvalidDataException($"No compatible app update artifact for {rid}.");
    ValidateArtifact(policy, app);

    string workRoot = UpdateStateStore.ResolveWorkRoot(policy);
    Directory.CreateDirectory(workRoot);
    string download = await DownloadVerifiedAsync(http, policy, app, workRoot, manifest.Version, CancellationToken.None);

    if (app.InstallMode.Equals("archive", StringComparison.OrdinalIgnoreCase))
    {
        string parent = Directory.GetParent(installRoot)?.FullName ?? throw new InvalidOperationException("Install root has no parent directory.");
        string stage = Path.Combine(parent, ".resone-stage-" + Guid.NewGuid().ToString("N"));
        backupRoot = Path.Combine(parent, ".resone-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            await ExtractVerifiedAsync(download, stage, policy, app, CancellationToken.None);
            PreserveConfiguredPaths(installRoot, stage, policy.PreservePaths);
            ValidateStagedRuntimeVersion(stage, manifest.Version);

            Directory.Move(installRoot, backupRoot);
            try
            {
                Directory.Move(stage, installRoot);
                appSwapped = true;
            }
            catch
            {
                if (!Directory.Exists(installRoot) && Directory.Exists(backupRoot)) Directory.Move(backupRoot, installRoot);
                throw;
            }
        }
        finally
        {
            TryDeleteDirectory(stage);
        }
    }
    else
    {
        await RunInstallerAsync(download, app, installRoot, aiRoot, CancellationToken.None);
    }

    if (policy.AutoInstallInstallerArtifacts)
    {
        foreach (UpdateArtifact extra in manifest.Artifacts.Where(a => a.Enabled && a.AutoInstall && !a.Kind.Equals("app", StringComparison.OrdinalIgnoreCase) && RidMatches(a.Rid, rid)))
        {
            try
            {
                ValidateArtifact(policy, extra);
                string artifact = await DownloadVerifiedAsync(http, policy, extra, workRoot, manifest.Version, CancellationToken.None);
                if (!extra.InstallMode.Equals("installer", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Non-app artifact {extra.Id} currently requires installMode=installer.");
                await RunInstallerAsync(artifact, extra, installRoot, aiRoot, CancellationToken.None);
            }
            catch (Exception error) when (!extra.Required)
            {
                ResoneDailyLog.Write("UPDATER", $"Optional artifact failed: {extra.Id}", error);
            }
        }
    }

    if (!TryLaunchLauncher(installRoot, policy, aiRoot, "--skip-update-once", "--updated-from", manifest.Version, out Process? launched, out string launchError))
        throw new InvalidOperationException("Updated launcher could not be started: " + launchError);

    await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(policy.RelaunchHealthSeconds, 1, 15)));
    if (launched is { HasExited: true })
        throw new InvalidOperationException($"Updated launcher exited immediately with code {launched.ExitCode}.");
    launched?.Dispose();

    await UpdateStateStore.RecordSuccessAsync(policy);
    if (backupRoot != null) TryDeleteDirectory(backupRoot);
    ResoneDailyLog.Write("UPDATER", $"Update to {manifest.Version} completed and launcher restarted.");
    return;
}
catch (Exception error)
{
    ResoneDailyLog.Write("UPDATER", "Update failed", error);
    try { await UpdateStateStore.RecordInstallFailureAsync(policy, targetVersion, error); } catch { }

    if (appSwapped && backupRoot != null && Directory.Exists(backupRoot))
    {
        try
        {
            string failed = installRoot + ".failed-" + Guid.NewGuid().ToString("N");
            if (Directory.Exists(installRoot)) Directory.Move(installRoot, failed);
            Directory.Move(backupRoot, installRoot);
            TryDeleteDirectory(failed);
            appSwapped = false;
            ResoneDailyLog.Write("UPDATER", "Rolled back to the previous Resone installation.");
        }
        catch (Exception rollbackError)
        {
            ResoneDailyLog.Write("UPDATER", "Rollback failed", rollbackError);
        }
    }

    if (!TryLaunchLauncher(installRoot, policy, aiRoot, "--skip-update-once", "--update-failed", null, out Process? fallback, out string relaunchError))
        WindowsTrayNotice.Show("Resone could not restart", "The update failed and Resone could not relaunch automatically. Start Resone again from its normal launcher. " + relaunchError);
    else fallback?.Dispose();

    Environment.ExitCode = 2;
}

static async Task<RuntimeUpdateEnvelope> LoadLocalConfigAsync(string installRoot, CancellationToken token)
{
    string path = Path.Combine(installRoot, "config", "runtime.json");
    if (!File.Exists(path)) return new();
    return JsonSerializer.Deserialize(await File.ReadAllTextAsync(path, token), UpdateJson.Default.RuntimeUpdateEnvelope) ?? new();
}

static void ConfigureLogging(LoggingPolicy policy, string installRoot)
{
    if (string.IsNullOrWhiteSpace(policy.Directory)) return;
    string path = Environment.ExpandEnvironmentVariables(policy.Directory.Trim());
    if (!Path.IsPathRooted(path)) path = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wds", path));
    ResoneDailyLog.Configure(path);
}

static async Task WaitForParentAsync(int pid, int timeoutSeconds)
{
    if (pid <= 0) return;
    try
    {
        using Process parent = Process.GetProcessById(pid);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 300)));
        await parent.WaitForExitAsync(stop.Token);
    }
    catch (ArgumentException) { }
}

static async Task<UpdateManifest> FetchManifestAsync(HttpClient http, UpdatePolicy policy, CancellationToken token)
{
    string manifestUrl = ExpandManifestUrl(policy);
    ValidateUrl(manifestUrl, policy.RequireHttps, "update manifest");
    using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
    stop.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(policy.CheckTimeoutSeconds, 2, 120)));
    using var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
    request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
    request.Headers.UserAgent.ParseAdd($"ResoneUpdater/{policy.CurrentVersion}");
    using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stop.Token);
    response.EnsureSuccessStatusCode();
    if (response.Content.Headers.ContentLength is long length && length > policy.ManifestMaxBytes) throw new InvalidDataException("Update manifest is too large.");
    await using var input = await response.Content.ReadAsStreamAsync(stop.Token);
    using var buffer = new MemoryStream();
    byte[] chunk = new byte[32 * 1024]; long total = 0; int n;
    while ((n = await input.ReadAsync(chunk.AsMemory(), stop.Token)) > 0)
    {
        total += n; if (total > policy.ManifestMaxBytes) throw new InvalidDataException("Update manifest is too large.");
        await buffer.WriteAsync(chunk.AsMemory(0, n), stop.Token);
    }
    buffer.Position = 0;
    return await JsonSerializer.DeserializeAsync(buffer, UpdateJson.Default.UpdateManifest, stop.Token)
        ?? throw new InvalidDataException("Update manifest was empty.");
}


static string ExpandManifestUrl(UpdatePolicy policy)
    => policy.ManifestUrl
        .Replace("{currentVersion}", Uri.EscapeDataString(policy.CurrentVersion), StringComparison.OrdinalIgnoreCase)
        .Replace("{channel}", Uri.EscapeDataString(policy.Channel), StringComparison.OrdinalIgnoreCase)
        .Replace("{rid}", Uri.EscapeDataString(CurrentRid()), StringComparison.OrdinalIgnoreCase);

static async Task<string> DownloadVerifiedAsync(HttpClient http, UpdatePolicy policy, UpdateArtifact artifact, string workRoot, string version, CancellationToken token)
{
    ValidateArtifact(policy, artifact);
    string dir = Path.Combine(workRoot, "downloads", Safe(version)); Directory.CreateDirectory(dir);
    string extension = artifact.InstallMode.Equals("installer", StringComparison.OrdinalIgnoreCase) ? Path.GetExtension(new Uri(artifact.Url).AbsolutePath) : ".zip";
    if (string.IsNullOrWhiteSpace(extension)) extension = artifact.InstallMode.Equals("installer", StringComparison.OrdinalIgnoreCase) ? ".exe" : ".zip";
    string destination = Path.Combine(dir, Safe(artifact.Id) + extension);
    Exception? last = null;
    int attempts = Math.Clamp(policy.DownloadAttempts, 1, 10);
    for (int attempt = 1; attempt <= attempts; attempt++)
    {
        try
        {
            TryDelete(destination);
            ResoneDailyLog.Write("UPDATER", $"Downloading {artifact.Id}, attempt {attempt}/{attempts}");
            using var response = await http.GetAsync(artifact.Url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            long max = artifact.MaxBytes > 0 ? artifact.MaxBytes : policy.DefaultArchiveMaxBytes;
            if (response.Content.Headers.ContentLength is long length && length > max) throw new InvalidDataException("Update download exceeds configured maximum size.");
            await using var input = await response.Content.ReadAsStreamAsync(token);
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            byte[] data = new byte[256 * 1024]; long total = 0; int n;
            while ((n = await input.ReadAsync(data.AsMemory(), token)) > 0)
            {
                total = checked(total + n); if (total > max) throw new InvalidDataException("Update download exceeds configured maximum size.");
                await output.WriteAsync(data.AsMemory(0, n), token);
            }
            await output.FlushAsync(token);
            if (total == 0) throw new InvalidDataException("Update download was empty.");
            await VerifyShaAsync(destination, artifact.Sha256, policy.RequireHashes, token);
            return destination;
        }
        catch (Exception error) when (attempt < attempts)
        {
            last = error; TryDelete(destination);
            ResoneDailyLog.Write("UPDATER", $"Download/verification attempt {attempt} failed for {artifact.Id}", error);
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(policy.RetryDelaySeconds, 1, 60) * attempt), token);
        }
        catch (Exception error) { last = error; break; }
    }
    TryDelete(destination);
    throw new InvalidDataException($"Update artifact {artifact.Id} failed after {attempts} attempts.", last);
}

static async Task ExtractVerifiedAsync(string archive, string stage, UpdatePolicy policy, UpdateArtifact artifact, CancellationToken token)
{
    long max = artifact.MaxExtractedBytes > 0 ? artifact.MaxExtractedBytes : policy.DefaultExtractedMaxBytes;
    long total = 0;
    using ZipArchive zip = ZipFile.OpenRead(archive);
    if (zip.Entries.Count > 100_000) throw new InvalidDataException("Update archive contains too many entries.");
    foreach (ZipArchiveEntry entry in zip.Entries)
    {
        token.ThrowIfCancellationRequested();
        if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Update archives may not contain symbolic links.");
        total = checked(total + entry.Length); if (total > max) throw new InvalidDataException("Expanded update exceeds configured size limit.");
        string[] parts = entry.FullName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= artifact.StripComponents) continue;
        string relative = string.Join('/', parts.Skip(artifact.StripComponents));
        string destination = Under(stage, relative);
        if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) { Directory.CreateDirectory(destination); continue; }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var input = entry.Open();
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output, token);
    }

    foreach (string required in artifact.RequiredFiles)
        if (!File.Exists(Under(stage, required)) && !Directory.Exists(Under(stage, required))) throw new InvalidDataException("Extracted update is missing required path: " + required);

    if (policy.VerifyExtractedFiles)
    {
        if (artifact.Files.Count == 0) throw new InvalidDataException("No extracted-file hashes were provided for the update.");
        foreach (UpdateFileHash file in artifact.Files)
        {
            string path = Under(stage, file.Path);
            if (!File.Exists(path)) throw new InvalidDataException("Hashed update file is missing: " + file.Path);
            await VerifyShaAsync(path, file.Sha256, true, token);
        }
    }
}

static void PreserveConfiguredPaths(string oldRoot, string stage, IEnumerable<string> paths)
{
    foreach (string relative in paths)
    {
        if (string.IsNullOrWhiteSpace(relative)) continue;
        string source = Under(oldRoot, relative); string destination = Under(stage, relative);
        if (File.Exists(source)) { Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination, true); }
        else if (Directory.Exists(source)) CopyDirectory(source, destination);
    }
}

static void ValidateStagedRuntimeVersion(string stage, string expected)
{
    string path = Path.Combine(stage, "config", "runtime.json");
    if (!File.Exists(path)) throw new InvalidDataException("Updated app payload is missing config/runtime.json.");
    try
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        string? version = doc.RootElement.TryGetProperty("updates", out var updates) && updates.TryGetProperty("currentVersion", out var current) ? current.GetString() : null;
        if (!string.Equals(version, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Updated app payload version {version ?? "<missing>"} does not match manifest version {expected}.");
    }
    catch (JsonException e) { throw new InvalidDataException("Updated runtime.json is invalid.", e); }
}

static async Task RunInstallerAsync(string path, UpdateArtifact artifact, string installRoot, string? aiRoot, CancellationToken token)
{
    var start = new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path)! };
    foreach (string arg in artifact.Arguments)
        start.ArgumentList.Add(arg.Replace("{installRoot}", installRoot).Replace("{aiRoot}", aiRoot ?? ""));
    using Process process = Process.Start(start) ?? throw new InvalidOperationException("Installer process could not be started: " + artifact.Id);
    await process.WaitForExitAsync(token);
    if (process.ExitCode != 0) throw new InvalidOperationException($"Installer {artifact.Id} failed with exit code {process.ExitCode}.");
}

static bool TryLaunchLauncher(string installRoot, UpdatePolicy policy, string? aiRoot, string firstArg, string? secondName, string? secondValue, out Process? process, out string error)
{
    process = null; error = "";
    try
    {
        string launcher = Path.GetFullPath(Path.Combine(installRoot, policy.LauncherExecutable));
        if (!File.Exists(launcher)) throw new FileNotFoundException("Launcher not found after update.", launcher);
        var start = new ProcessStartInfo(launcher) { UseShellExecute = false, WorkingDirectory = installRoot };
        start.ArgumentList.Add(firstArg);
        if (!string.IsNullOrWhiteSpace(secondName)) { start.ArgumentList.Add(secondName); if (!string.IsNullOrWhiteSpace(secondValue)) start.ArgumentList.Add(secondValue); }
        if (!string.IsNullOrWhiteSpace(aiRoot)) { start.ArgumentList.Add("--ai-root"); start.ArgumentList.Add(aiRoot); }
        process = Process.Start(start) ?? throw new InvalidOperationException("Process.Start returned null.");
        return true;
    }
    catch (Exception e) { error = e.Message; return false; }
}
static async Task VerifyShaAsync(string path, string expected, bool required, CancellationToken token)
{
    if (string.IsNullOrWhiteSpace(expected)) { if (required) throw new InvalidDataException("SHA256 is required."); return; }
    if (expected.Length != 64 || !expected.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid SHA256 metadata.");
    await using var input = File.OpenRead(path);
    string actual = Convert.ToHexString(await SHA256.HashDataAsync(input, token));
    if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"SHA256 mismatch for {Path.GetFileName(path)}. Expected {expected}; got {actual}.");
}

static void ValidateArtifact(UpdatePolicy policy, UpdateArtifact artifact)
{
    ValidateUrl(artifact.Url, policy.RequireHttps, "update artifact");
    if (policy.RequireHashes && (artifact.Sha256.Length != 64 || !artifact.Sha256.All(Uri.IsHexDigit))) throw new InvalidDataException($"Artifact {artifact.Id} requires SHA256.");
    if (!artifact.InstallMode.Equals("archive", StringComparison.OrdinalIgnoreCase) && !artifact.InstallMode.Equals("installer", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("installMode must be archive or installer.");
    if (artifact.StripComponents < 0 || artifact.StripComponents > 8) throw new InvalidDataException("stripComponents must be 0-8.");
}
static void ValidateUrl(string value, bool requireHttps, string label)
{
    if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)) throw new InvalidDataException("Invalid " + label + " URL.");
    if (requireHttps && uri.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException(label + " URL must use HTTPS.");
}
static string CurrentRid()
{
    if (OperatingSystem.IsWindows()) return Environment.Is64BitProcess ? "win-x64" : "win-x86";
    if (OperatingSystem.IsMacOS()) return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "osx-arm64" : "osx-x64";
    if (OperatingSystem.IsLinux()) return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "linux-arm64" : "linux-x64";
    return System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
}
static bool RidMatches(string configured, string actual) => string.IsNullOrWhiteSpace(configured) || configured == "*" || configured.Equals(actual, StringComparison.OrdinalIgnoreCase);
static string Safe(string value) => new(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.').ToArray());
static string Under(string root, string relative)
{
    if (Path.IsPathRooted(relative)) throw new InvalidDataException("Update path must be relative.");
    string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    string candidate = Path.GetFullPath(Path.Combine(rootFull, relative.Replace('/', Path.DirectorySeparatorChar)));
    StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    if (!candidate.StartsWith(rootFull + Path.DirectorySeparatorChar, comparison)) throw new InvalidDataException("Update path escapes its root: " + relative);
    return candidate;
}
static void CopyDirectory(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
    foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        string dest = Path.Combine(destination, Path.GetRelativePath(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(file, dest, true);
    }
}
static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
