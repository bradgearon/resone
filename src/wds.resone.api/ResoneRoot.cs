namespace Wds.Resone.Api;

/// <summary>
/// Resolves the active Resone runtime tree. Development builds prefer the tree
/// containing config/appsettings.json plus build outputs; installed builds fall
/// back to %LOCALAPPDATA%/Wds/Resone. User-writable state is intentionally not
/// stored here and remains under LocalApplicationData/Wds/Resone/user.
/// </summary>
public static class ResoneRoot
{
    public static string Resolve(string? start = null)
    {
        string? explicitHome = Environment.GetEnvironmentVariable("RESONE_HOME");
        if (!string.IsNullOrWhiteSpace(explicitHome))
            return Path.GetFullPath(explicitHome);

        foreach (string candidateStart in Starts(start))
        {
            string? found = SearchParents(candidateStart);
            if (found is not null)
                return found;
        }

        string installed = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Wds", "Resone");
        if (LooksLikeRuntimeRoot(installed))
            return Path.GetFullPath(installed);

        return Path.GetFullPath(start ?? AppContext.BaseDirectory);
    }

    public static string LauncherExecutable(string root)
    {
        string name = OperatingSystem.IsWindows() ? "wds.resone.launcher.exe" : "wds.resone.launcher";
        string[] candidates =
        [
            Path.Combine(root, name),
            Path.Combine(root, "build", "launcher", name)
        ];
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException(
                "Resone launcher was not found under the active runtime root. " +
                "Expected it beside the installed app or under build/launcher.", candidates[0]);
    }

    private static IEnumerable<string> Starts(string? start)
    {
        if (!string.IsNullOrWhiteSpace(start)) yield return start;
        if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory)) yield return AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(Environment.CurrentDirectory)) yield return Environment.CurrentDirectory;
        string? process = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(process))
        {
            string? directory = Path.GetDirectoryName(process);
            if (!string.IsNullOrWhiteSpace(directory)) yield return directory;
        }
    }

    private static string? SearchParents(string start)
    {
        DirectoryInfo? cursor;
        try { cursor = new DirectoryInfo(Path.GetFullPath(start)); }
        catch { return null; }
        for (int i = 0; i < 10 && cursor is not null; i++, cursor = cursor.Parent)
            if (LooksLikeRuntimeRoot(cursor.FullName))
                return cursor.FullName;
        return null;
    }

    private static bool LooksLikeRuntimeRoot(string root)
    {
        if (!File.Exists(Path.Combine(root, "config", "appsettings.json")))
            return false;
        string api = Path.Combine(root, "wds.resone.api.dll");
        string devApi = Path.Combine(root, "build", "api", "wds.resone.api.dll");
        return File.Exists(api) || File.Exists(devApi);
    }
}
