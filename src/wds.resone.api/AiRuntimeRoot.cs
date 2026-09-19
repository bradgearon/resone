namespace Wds.Resone.Api;

/// <summary>
/// Shared machine-local root for large AI engines and models. Resone and Six Stars
/// can point at the same tree by setting AI_ROOT. The launcher persists the user's
/// choice, but API/runtime code only needs to resolve the effective root.
/// </summary>
public static class AiRuntimeRoot
{
    public const string EnvironmentVariable = "AI_ROOT";
    public const string LegacyEnvironmentVariable = "WDS_AI_ROOT";

    public static string PreferenceFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Wds", "AI", "root.txt");

    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Wds", "AI");

    public static string Resolve()
    {
        string? explicitRoot = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(explicitRoot))
            explicitRoot = Environment.GetEnvironmentVariable(LegacyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitRoot))
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(explicitRoot.Trim()));

        try
        {
            if (File.Exists(PreferenceFile))
            {
                string saved = File.ReadAllText(PreferenceFile).Trim();
                if (!string.IsNullOrWhiteSpace(saved))
                    return Path.GetFullPath(Environment.ExpandEnvironmentVariables(saved));
            }
        }
        catch { /* preference lookup is best effort; launcher reports persistence failures */ }

        return Path.GetFullPath(DefaultRoot);
    }

    public static string ResolveAsset(string path)
    {
        if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
        string shared = Path.GetFullPath(Path.Combine(Resolve(), path));
        if (File.Exists(shared) || Directory.Exists(shared)) return shared;
        // Development/source-tree compatibility while projects migrate to AI_ROOT.
        string legacy = Path.GetFullPath(Path.Combine(ResoneRoot.Resolve(), path));
        if (File.Exists(legacy) || Directory.Exists(legacy)) return legacy;
        return shared;
    }

    public static string ResolveEngineDirectory(string component, string configuredFallback)
    {
        string root = Resolve();
        string pointer = Path.Combine(root, "engines", component, ".active");
        try
        {
            if (File.Exists(pointer))
            {
                string relative = File.ReadAllText(pointer).Trim();
                if (!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Split('/', '\\').Any(x => x == ".." || x.Contains(':')))
                {
                    string active = Path.GetFullPath(Path.Combine(root, relative));
                    string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (active.StartsWith(rootFull + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) && Directory.Exists(active))
                        return active;
                }
            }
        }
        catch { }
        return ResolveAsset(configuredFallback);
    }
}

