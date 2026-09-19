using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

namespace Wds.Resone.Api.Ai;

/// <summary>One file per model call; diagnostic failures never fail composition.</summary>
public sealed class LlmRequestLog : IDisposable
{
    private readonly string? path = null;
    private readonly JsonObject record;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly StringBuilder raw = new();
    public LlmRequestLog(ResoneSettings settings, string label, JsonObject request, string? endpoint = null)
    {
        record = new JsonObject { ["label"] = label, ["startedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["endpoint"] = endpoint ?? settings.LlmUrl, ["request"] = request.DeepClone(), ["status"] = "pending" };
        #if RESONE_CUSTOMER_RELEASE
        return;
#else
        if (!settings.LogLlmRequests) return;
        var directory = ResolveDirectory(settings);
        try
        {
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, $"llm-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.json");
            Write();
        }
        catch (Exception e)
        {
            throw new IOException($"LLM logging is enabled but cannot write to {directory}: {e.Message}", e);
        }
#endif
    }
    public static string ResolveDirectory(ResoneSettings settings) =>
        Path.GetFullPath(Path.Combine(FindRoot(), settings.LlmLogDirectory));

    public static string Probe(ResoneSettings settings, string configPath)
    {
        string directory = ResolveDirectory(settings);
        if (!settings.LogLlmRequests) return "LLM logging DISABLED. Config: " + configPath;
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, $"launcher-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Environment.ProcessId}.log");
        File.WriteAllText(file, $"Config: {configPath}\nProcess: {Environment.ProcessPath}\nPID: {Environment.ProcessId}\nLLM logs: {directory}\n", new UTF8Encoding(false));
        return "LLM logging enabled; write test passed. Folder: " + directory + ". Config: " + configPath;
    }
    private static string FindRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "Wds.Resone.sln"))) return d.FullName;
        return Environment.GetEnvironmentVariable("RESONE_HOME") ?? AppContext.BaseDirectory;
    }
    public void Raw(string value) { if (path != null) raw.AppendLine(value); }
    public void HttpStatus(int status) { record["httpStatus"] = status; }
    public void Complete(string content) { record["response"] = content; record["status"] = "completed"; }
    public void Fail(Exception error, string partial)
    {
        record["status"] = error is OperationCanceledException ? "cancelled" : "failed";
        record["error"] = error.ToString(); record["response"] = partial;
    }
    private void Write()
    {
        if (path != null)
            File.WriteAllText(path, record.ToJsonString(new System.Text.Json.JsonSerializerOptions(ResoneJson.Default.Options) { WriteIndented = true }), new UTF8Encoding(false));
    }
    private void Save()
    {
        if (path == null) return;
        try { Write(); }
        catch (Exception e) { Console.Error.WriteLine("LLM log write failed: " + e.Message); }
    }
    public void Dispose()
    {
        record["elapsedMilliseconds"] = clock.ElapsedMilliseconds;
        if (path != null) record["rawResponse"] = raw.ToString();
        Save();
    }
}
