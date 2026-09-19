using System.Text;

namespace Wds.Resone.Api;

/// <summary>
/// Cross-process, human-readable Resone log. All processes append to one file per local day.
/// </summary>
public static class ResoneDailyLog
{
    public const string DirectoryEnvironmentVariable = "RESONE_LOG_DIR";
    private static readonly Mutex Gate = new(false, OperatingSystem.IsWindows() ? @"Local\Wds.Resone.Log" : "Wds.Resone.Log");
    private static string? configuredDirectory;

    public static void Configure(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return;
        configuredDirectory = Path.GetFullPath(Environment.ExpandEnvironmentVariables(directory));
        Environment.SetEnvironmentVariable(DirectoryEnvironmentVariable, configuredDirectory);
    }

    public static string DirectoryPath
    {
        get
        {
            string? value = configuredDirectory ?? Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(value)) return Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(local)) local = Path.GetTempPath();
            return Path.Combine(local, "Wds", "Logs", "Resone");
        }
    }

    public static string CurrentPath => Path.Combine(DirectoryPath, $"resone-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Write(string area, string message, Exception? error = null)
    {
        string line = $"{DateTimeOffset.Now:O}\t{Environment.ProcessId}\t{area}\t{message}";
        if (error != null) line += $"\t{error}";
        Append(line);
    }

    public static void WriteBlock(string area, string header, string? body)
    {
        var text = new StringBuilder();
        text.Append(DateTimeOffset.Now.ToString("O")).Append('\t').Append(Environment.ProcessId).Append('\t').Append(area).Append('\t').Append(header).AppendLine();
        if (!string.IsNullOrEmpty(body)) text.AppendLine(body.TrimEnd());
        text.AppendLine("---");
        Append(text.ToString().TrimEnd('\r', '\n'));
    }

    private static void Append(string text)
    {
        bool held = false;
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            try { held = Gate.WaitOne(TimeSpan.FromSeconds(5)); } catch (AbandonedMutexException) { held = true; }
            using var stream = new FileStream(CurrentPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.WriteLine(text);
        }
        catch (Exception e)
        {
            try { Console.Error.WriteLine("Resone log unavailable: " + e.Message); } catch { }
        }
        finally
        {
            if (held) try { Gate.ReleaseMutex(); } catch { }
        }
    }
}
