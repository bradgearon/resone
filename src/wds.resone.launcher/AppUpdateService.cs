using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Wds.Resone.Api;
using Wds.Resone.Api.Updates;

namespace Wds.Resone.Launcher;

internal sealed class AppUpdateService(string installRoot, HttpClient http, TrayIcon tray)
{
    public async Task<bool> TryStartUpdateAsync(string[] args, string aiRoot, CancellationToken token)
    {
        RuntimeUpdateEnvelope envelope = await LoadLocalConfigAsync(token).ConfigureAwait(false);
        UpdatePolicy policy = envelope.Updates;
        ConfigureLogging(envelope.Logging);
        if (!policy.Enabled) return false;

        UpdateFailureState state = await UpdateStateStore.LoadAsync(policy, token).ConfigureAwait(false);
        if (state.ConsecutiveInstallFailures >= Math.Max(1, policy.NotifyAfterConsecutiveFailures))
            tray.Notify("Resone update needs attention", $"Resone has failed to update {state.ConsecutiveInstallFailures} times. It will keep retrying on later launches. {state.LastError}", true);

        if (args.Contains("--skip-update-once", StringComparer.OrdinalIgnoreCase) || args.Contains("--no-update", StringComparer.OrdinalIgnoreCase))
            return false;

        bool force = args.Contains("--force-update-check", StringComparer.OrdinalIgnoreCase);
        if (!force && state.LastFailureUtc is DateTimeOffset last && state.ConsecutiveInstallFailures > 0 &&
            DateTimeOffset.UtcNow - last < TimeSpan.FromMinutes(Math.Max(1, policy.FailedUpdateRetryMinutes)))
            return false;

        try
        {
            UpdateManifest manifest = await FetchManifestAsync(policy, token).ConfigureAwait(false);
            if (!manifest.Product.Equals("resone", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Update manifest product is not Resone.");
            if (!string.IsNullOrWhiteSpace(policy.Channel) && !manifest.Channel.Equals(policy.Channel, StringComparison.OrdinalIgnoreCase))
                return false;
            if (UpdateVersion.Compare(manifest.Version, policy.CurrentVersion) <= 0)
            {
                if (state.ConsecutiveCheckFailures != 0)
                {
                    state.ConsecutiveCheckFailures = 0;
                    await UpdateStateStore.SaveAsync(policy, state, token).ConfigureAwait(false);
                }
                return false;
            }

            string rid = CurrentRid();
            UpdateArtifact? app = manifest.Artifacts.FirstOrDefault(a => a.Enabled && a.Kind.Equals("app", StringComparison.OrdinalIgnoreCase) && RidMatches(a.Rid, rid));
            if (app is null) throw new InvalidDataException($"Update {manifest.Version} has no compatible app artifact for {rid}.");
            ValidateArtifact(policy, app);

            string installedUpdater = Path.GetFullPath(Path.Combine(installRoot, policy.UpdaterExecutable));
            if (!File.Exists(installedUpdater)) throw new FileNotFoundException("Resone updater is missing.", installedUpdater);
            string work = UpdateStateStore.ResolveWorkRoot(policy);
            string runner = Path.Combine(work, "runner-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runner);
            string runnerExe = Path.Combine(runner, Path.GetFileName(policy.UpdaterExecutable));
            File.Copy(installedUpdater, runnerExe, true);
            string installedIcon = Path.Combine(installRoot, "Resone.ico");
            if (File.Exists(installedIcon)) File.Copy(installedIcon, Path.Combine(runner, "Resone.ico"), true);

            var start = new ProcessStartInfo(runnerExe)
            {
                UseShellExecute = false,
                WorkingDirectory = runner,
                CreateNoWindow = true
            };
            Add(start, "--install-root", installRoot);
            Add(start, "--parent-pid", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Add(start, "--target-version", manifest.Version);
            Add(start, "--ai-root", aiRoot);
            Process.Start(start)?.Dispose();
            ResoneDailyLog.Write("UPDATE", $"Updater started for {policy.CurrentVersion} -> {manifest.Version}");
            return true;
        }
        catch (Exception error)
        {
            ResoneDailyLog.Write("UPDATE", "Update check failed", error);
            await UpdateStateStore.RecordCheckFailureAsync(policy, error, token).ConfigureAwait(false);
            state = await UpdateStateStore.LoadAsync(policy, token).ConfigureAwait(false);
            if (state.ConsecutiveCheckFailures >= Math.Max(1, policy.NotifyAfterConsecutiveFailures))
                tray.Notify("Resone update check is failing", $"Resone could not check for updates {state.ConsecutiveCheckFailures} times. {error.Message}", true);
            return false;
        }
    }

    private async Task<RuntimeUpdateEnvelope> LoadLocalConfigAsync(CancellationToken token)
    {
        string path = Path.Combine(installRoot, "config", "runtime.json");
        if (!File.Exists(path)) return new();
        return JsonSerializer.Deserialize(await File.ReadAllTextAsync(path, token).ConfigureAwait(false), UpdateJson.Default.RuntimeUpdateEnvelope) ?? new();
    }

    private void ConfigureLogging(LoggingPolicy policy)
    {
        if (!string.IsNullOrWhiteSpace(policy.Directory))
        {
            string path = Environment.ExpandEnvironmentVariables(policy.Directory.Trim());
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(Path.Combine(installRoot, path));
            ResoneDailyLog.Configure(path);
        }
    }

    private async Task<UpdateManifest> FetchManifestAsync(UpdatePolicy policy, CancellationToken token)
    {
        string manifestUrl = ExpandManifestUrl(policy);
        ValidateUrl(manifestUrl, policy.RequireHttps, "update manifest");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        stop.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(policy.CheckTimeoutSeconds, 2, 120)));
        using var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        request.Headers.UserAgent.ParseAdd($"ResoneLauncher/{policy.CurrentVersion}");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stop.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length > policy.ManifestMaxBytes) throw new InvalidDataException("Update manifest is too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(stop.Token).ConfigureAwait(false);
        using var limited = new MemoryStream();
        byte[] buffer = new byte[32 * 1024]; int n; long total = 0;
        while ((n = await stream.ReadAsync(buffer.AsMemory(), stop.Token).ConfigureAwait(false)) > 0)
        {
            total += n; if (total > policy.ManifestMaxBytes) throw new InvalidDataException("Update manifest is too large.");
            await limited.WriteAsync(buffer.AsMemory(0, n), stop.Token).ConfigureAwait(false);
        }
        limited.Position = 0;
        return await JsonSerializer.DeserializeAsync(limited, UpdateJson.Default.UpdateManifest, stop.Token).ConfigureAwait(false)
            ?? throw new InvalidDataException("Update manifest was empty.");
    }


    private static string ExpandManifestUrl(UpdatePolicy policy)
        => policy.ManifestUrl
            .Replace("{currentVersion}", Uri.EscapeDataString(policy.CurrentVersion), StringComparison.OrdinalIgnoreCase)
            .Replace("{channel}", Uri.EscapeDataString(policy.Channel), StringComparison.OrdinalIgnoreCase)
            .Replace("{rid}", Uri.EscapeDataString(CurrentRid()), StringComparison.OrdinalIgnoreCase);

    internal static void ValidateArtifact(UpdatePolicy policy, UpdateArtifact artifact)
    {
        if (string.IsNullOrWhiteSpace(artifact.Id)) throw new InvalidDataException("Update artifact id is required.");
        ValidateUrl(artifact.Url, policy.RequireHttps, "update artifact");
        if (policy.RequireHashes && (artifact.Sha256.Length != 64 || !artifact.Sha256.All(Uri.IsHexDigit)))
            throw new InvalidDataException($"Update artifact {artifact.Id} requires a SHA256 hash.");
        if (!artifact.InstallMode.Equals("archive", StringComparison.OrdinalIgnoreCase) && !artifact.InstallMode.Equals("installer", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("installMode must be archive or installer.");
        if (artifact.StripComponents < 0 || artifact.StripComponents > 8) throw new InvalidDataException("stripComponents must be 0-8.");
        if (policy.VerifyExtractedFiles && artifact.InstallMode == "archive" && artifact.Files.Count == 0)
            throw new InvalidDataException($"Update artifact {artifact.Id} must provide extracted file hashes when verifyExtractedFiles is enabled.");
        foreach (var file in artifact.Files)
            if (file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException("Every extracted update file hash must be SHA256.");
    }

    internal static string CurrentRid()
    {
        if (OperatingSystem.IsWindows()) return Environment.Is64BitProcess ? "win-x64" : "win-x86";
        if (OperatingSystem.IsMacOS()) return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        if (OperatingSystem.IsLinux()) return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        return RuntimeInformation.RuntimeIdentifier;
    }
    internal static bool RidMatches(string configured, string actual) => string.IsNullOrWhiteSpace(configured) || configured == "*" || configured.Equals(actual, StringComparison.OrdinalIgnoreCase);
    internal static void ValidateUrl(string value, bool requireHttps, string label)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)) throw new InvalidDataException($"Invalid {label} URL.");
        if (requireHttps && uri.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException($"{label} URL must use HTTPS.");
    }
    private static void Add(ProcessStartInfo start, string name, string value) { start.ArgumentList.Add(name); start.ArgumentList.Add(value); }
}
