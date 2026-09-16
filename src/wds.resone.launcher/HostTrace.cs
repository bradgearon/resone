using System.Text;
using System.Text.Json.Nodes;
using Wds.Resone.Api;
using Wds.Resone.Api.Ai;

namespace Wds.Resone.Launcher;

/// <summary>Traces requests before validation or model preparation can fail.</summary>
internal sealed class HostTrace
{
    private readonly string? path;
    private readonly object gate = new();
    public HostTrace(ResoneSettings settings)
    {
        if (!settings.LogLlmRequests) return;
        try
        {
            var directory = LlmRequestLog.ResolveDirectory(settings);
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, $"host-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Environment.ProcessId}.jsonl");
        }
        catch (Exception e) { Console.Error.WriteLine("Host trace unavailable: " + e.Message); }
    }
    public void Write(string stage, string id = "", string detail = "")
    {
        if (path == null) return;
        try
        {
            var entry = new JsonObject { ["utc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["pid"] = Environment.ProcessId, ["stage"] = stage,
                ["requestId"] = id, ["detail"] = detail };
            lock (gate) File.AppendAllText(path, entry.ToJsonString() + "\n", new UTF8Encoding(false));
        }
        catch (Exception e) { Console.Error.WriteLine("Host trace failed: " + e.Message); }
    }
}
