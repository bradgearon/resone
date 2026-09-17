using System.Text;

namespace Wds.Resone.Launcher;

/// <summary>
/// Always-on startup diagnostics for native runtime loading. It intentionally
/// records no prompts or model output, only paths/settings/stages/errors so a
/// worker that fails before the request logger exists is still diagnosable.
/// Logging failure itself must never prevent Resone from starting.
/// </summary>
internal sealed class StartupTrace : IDisposable
{
    private StreamWriter? writer;
    private readonly object gate = new();
    public string Path { get; private set; } = "unavailable";

    public StartupTrace(string root)
    {
        foreach (string directory in CandidateDirectories(root))
        {
            try
            {
                Directory.CreateDirectory(directory);
                string path = System.IO.Path.Combine(directory,
                    $"startup-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Environment.ProcessId}.log");
                writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
                    new UTF8Encoding(false)) { AutoFlush = true };
                Path = path;
                return;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Startup trace unavailable at {directory}: {e.Message}");
            }
        }
    }

    private static IEnumerable<string> CandidateDirectories(string root)
    {
        yield return System.IO.Path.Combine(root, "logs");
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
            yield return System.IO.Path.Combine(local, "Wds", "Resone", "logs");
        yield return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Resone", "logs");
    }

    public void Write(string stage, string detail = "")
    {
        lock (gate)
        {
            try { writer?.WriteLine($"{DateTimeOffset.UtcNow:O}\t{stage}\t{detail}"); }
            catch (Exception e) { Console.Error.WriteLine("Startup trace write failed: " + e.Message); }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            try { writer?.Dispose(); }
            catch { }
            writer = null;
        }
    }
}
