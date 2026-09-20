using System.Text.Json;

namespace Wds.Resone.Api;

public sealed record InstallationPreferences(
    string? InstallRoot,
    string? AiRoot,
    bool SetAiRoot,
    bool InstallVst3,
    string? Vst3Root)
{
    public static string PreferenceFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "WDS", "Resone", "install.json");

    public static InstallationPreferences Load()
    {
        try
        {
            if (!File.Exists(PreferenceFile))
                return new(null, null, false, true, null);

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(PreferenceFile));
            JsonElement root = doc.RootElement;
            return new(
                Normalize(ReadString(root, "installRoot")),
                Normalize(ReadString(root, "aiRoot")),
                ReadBool(root, "setAiRoot", false),
                ReadBool(root, "installVst3", true),
                Normalize(ReadString(root, "vst3Root")));
        }
        catch
        {
            return new(null, null, false, true, null);
        }
    }

    public string ResolveInstallRoot(string fallback)
        => !string.IsNullOrWhiteSpace(InstallRoot) ? InstallRoot! : Path.GetFullPath(fallback);

    public string ResolveAiRoot()
        => !string.IsNullOrWhiteSpace(AiRoot) ? AiRoot! : AiRuntimeRoot.Resolve();

    public string? ResolveVst3Root()
        => string.IsNullOrWhiteSpace(Vst3Root) ? null : Vst3Root;

    private static string? ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool ReadBool(JsonElement root, string name, bool fallback)
        => root.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'))); }
        catch { return null; }
    }
}
