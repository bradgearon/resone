using Wds.Resone.Api;

namespace Wds.Resone.Launcher;

/// <summary>Always-on startup diagnostics written into the shared daily Resone log.</summary>
internal sealed class StartupTrace : IDisposable
{
    public string Path => ResoneDailyLog.CurrentPath;
    public StartupTrace(string root) => ResoneDailyLog.Write("STARTUP", "launcher startup trace initialized");
    public void Write(string stage, string detail = "") => ResoneDailyLog.Write("STARTUP", $"{stage}: {detail}");
    public void Dispose() { }
}
