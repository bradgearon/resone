using System.Text.Json.Serialization;

namespace Wds.Resone.Api.Updates;

public sealed class UpdatePolicy
{
    public bool Enabled { get; set; }
    public string CurrentVersion { get; set; } = "0.1.0";
    public string Channel { get; set; } = "stable";
    public string ManifestUrl { get; set; } = "https://resone.io/api/updates/resone/stable.json";
    public int CheckTimeoutSeconds { get; set; } = 8;
    public int DownloadAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
    public int FailedUpdateRetryMinutes { get; set; } = 15;
    public int ParentExitTimeoutSeconds { get; set; } = 60;
    public int RelaunchHealthSeconds { get; set; } = 2;
    public int NotifyAfterConsecutiveFailures { get; set; } = 3;
    public long ManifestMaxBytes { get; set; } = 2 * 1024 * 1024;
    public long DefaultArchiveMaxBytes { get; set; } = 2L * 1024 * 1024 * 1024;
    public long DefaultExtractedMaxBytes { get; set; } = 4L * 1024 * 1024 * 1024;
    public string UpdaterExecutable { get; set; } = "wds.resone.updater.exe";
    public string LauncherExecutable { get; set; } = "wds.resone.launcher.exe";
    public string UpdateWorkDirectory { get; set; } = "updates";
    public bool RequireHttps { get; set; } = true;
    public bool RequireHashes { get; set; } = true;
    public bool VerifyExtractedFiles { get; set; } = true;
    public bool AutoInstallInstallerArtifacts { get; set; } = true;
    public List<string> PreservePaths { get; set; } = ["user"];
}

public sealed class LoggingPolicy
{
    public string Directory { get; set; } = "";
    public int RetentionDays { get; set; } = 14;
}


public sealed class RuntimeUpdateEnvelope
{
    public UpdatePolicy Updates { get; set; } = new();
    public LoggingPolicy Logging { get; set; } = new();
}

public sealed class UpdateManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Product { get; set; } = "resone";
    public string Channel { get; set; } = "stable";
    public string Version { get; set; } = "";
    public string? PublishedUtc { get; set; }
    public string? ReleaseNotesUrl { get; set; }
    public List<UpdateArtifact> Artifacts { get; set; } = [];
}

public sealed class UpdateArtifact
{
    public bool Enabled { get; set; } = true;
    public string Id { get; set; } = "app";
    public string Kind { get; set; } = "app"; // app, vst3, optional component
    public string Rid { get; set; } = "win-x64";
    public string InstallMode { get; set; } = "archive"; // archive or installer
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long MaxBytes { get; set; }
    public long MaxExtractedBytes { get; set; }
    public int StripComponents { get; set; }
    public bool Required { get; set; } = true;
    public bool AutoInstall { get; set; } = true;
    public List<string> RequiredFiles { get; set; } = [];
    public List<UpdateFileHash> Files { get; set; } = [];
    public List<string> Arguments { get; set; } = [];
}

public sealed class UpdateFileHash
{
    public string Path { get; set; } = "";
    public string Sha256 { get; set; } = "";
}

public sealed class UpdateFailureState
{
    public int ConsecutiveCheckFailures { get; set; }
    public int ConsecutiveInstallFailures { get; set; }
    public string LastError { get; set; } = "";
    public string LastTargetVersion { get; set; } = "";
    public DateTimeOffset? LastFailureUtc { get; set; }
    public DateTimeOffset? LastSuccessUtc { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(RuntimeUpdateEnvelope))]
[JsonSerializable(typeof(UpdateManifest))]
[JsonSerializable(typeof(UpdateArtifact))]
[JsonSerializable(typeof(UpdateFileHash))]
[JsonSerializable(typeof(UpdateFailureState))]
[JsonSerializable(typeof(UpdatePolicy))]
[JsonSerializable(typeof(LoggingPolicy))]
public partial class UpdateJson : JsonSerializerContext;

public static class UpdateVersion
{
    public static int Compare(string left, string right)
    {
        static (int[] parts, string suffix) Parse(string value)
        {
            string[] split = (value ?? "").Trim().TrimStart('v', 'V').Split('-', 2);
            int[] parts = split[0].Split('.', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out int n) ? n : 0).ToArray();
            return (parts, split.Length > 1 ? split[1] : "");
        }
        var a = Parse(left); var b = Parse(right);
        int n = Math.Max(a.parts.Length, b.parts.Length);
        for (int i = 0; i < n; i++)
        {
            int av = i < a.parts.Length ? a.parts[i] : 0;
            int bv = i < b.parts.Length ? b.parts[i] : 0;
            int c = av.CompareTo(bv); if (c != 0) return c;
        }
        if (a.suffix.Length == 0 && b.suffix.Length != 0) return 1;
        if (a.suffix.Length != 0 && b.suffix.Length == 0) return -1;
        return string.Compare(a.suffix, b.suffix, StringComparison.OrdinalIgnoreCase);
    }
}

public static class UpdateStateStore
{
    public static string ResolveWorkRoot(UpdatePolicy policy)
    {
        string value = string.IsNullOrWhiteSpace(policy.UpdateWorkDirectory) ? "updates" : policy.UpdateWorkDirectory.Trim();
        if (Path.IsPathRooted(value)) return Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local)) local = Path.GetTempPath();
        return Path.GetFullPath(Path.Combine(local, "Wds", value));
    }

    public static string StatePath(UpdatePolicy policy) => Path.Combine(ResolveWorkRoot(policy), "update-state.json");

    public static async Task<UpdateFailureState> LoadAsync(UpdatePolicy policy, CancellationToken token = default)
    {
        string path = StatePath(policy);
        if (!File.Exists(path)) return new();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize(await File.ReadAllTextAsync(path, token).ConfigureAwait(false), UpdateJson.Default.UpdateFailureState) ?? new();
        }
        catch { return new(); }
    }

    public static async Task SaveAsync(UpdatePolicy policy, UpdateFailureState state, CancellationToken token = default)
    {
        string path = StatePath(policy);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temp, System.Text.Json.JsonSerializer.Serialize(state, UpdateJson.Default.UpdateFailureState), token).ConfigureAwait(false);
        File.Move(temp, path, true);
    }

    public static async Task RecordCheckFailureAsync(UpdatePolicy policy, Exception error, CancellationToken token = default)
    {
        var state = await LoadAsync(policy, token).ConfigureAwait(false);
        state.ConsecutiveCheckFailures++;
        state.LastError = error.Message;
        state.LastFailureUtc = DateTimeOffset.UtcNow;
        await SaveAsync(policy, state, token).ConfigureAwait(false);
    }

    public static async Task RecordInstallFailureAsync(UpdatePolicy policy, string targetVersion, Exception error, CancellationToken token = default)
    {
        var state = await LoadAsync(policy, token).ConfigureAwait(false);
        state.ConsecutiveInstallFailures++;
        state.LastTargetVersion = targetVersion;
        state.LastError = error.Message;
        state.LastFailureUtc = DateTimeOffset.UtcNow;
        await SaveAsync(policy, state, token).ConfigureAwait(false);
    }

    public static async Task RecordSuccessAsync(UpdatePolicy policy, CancellationToken token = default)
    {
        var state = await LoadAsync(policy, token).ConfigureAwait(false);
        state.ConsecutiveCheckFailures = 0;
        state.ConsecutiveInstallFailures = 0;
        state.LastError = "";
        state.LastTargetVersion = "";
        state.LastSuccessUtc = DateTimeOffset.UtcNow;
        await SaveAsync(policy, state, token).ConfigureAwait(false);
    }
}
