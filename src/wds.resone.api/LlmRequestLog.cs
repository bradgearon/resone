using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

namespace Wds.Resone.Api.Ai;

/// <summary>Diagnostic model-call logging into the single daily Resone text log.</summary>
public sealed class LlmRequestLog : IDisposable
{
    private readonly bool enabled;
    private readonly string label;
    private readonly JsonObject record;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly StringBuilder raw = new();
    public LlmRequestLog(ResoneSettings settings, string label, JsonObject request, string? endpoint = null)
    {
        this.label = label;
        record = new JsonObject { ["label"] = label, ["startedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["endpoint"] = endpoint ?? settings.LlmUrl, ["request"] = request.DeepClone(), ["status"] = "pending" };
#if RESONE_CUSTOMER_RELEASE
        enabled = false;
#else
        enabled = settings.LogLlmRequests;
        if (enabled) ResoneDailyLog.WriteBlock("LLM", $"BEGIN {label}", record.ToJsonString(new System.Text.Json.JsonSerializerOptions(ResoneJson.Default.Options) { WriteIndented = true }));
#endif
    }
    public static string ResolveDirectory(ResoneSettings settings) => ResoneDailyLog.DirectoryPath;
    public static string Probe(ResoneSettings settings, string configPath)
    {
        if (!settings.LogLlmRequests) return "LLM logging DISABLED. Config: " + configPath;
        ResoneDailyLog.Write("LOG", $"Logging probe passed. Config={configPath}; Process={Environment.ProcessPath}; PID={Environment.ProcessId}");
        return "LLM logging enabled; shared daily log: " + ResoneDailyLog.CurrentPath + ". Config: " + configPath;
    }
    public void Raw(string value) { if (enabled) raw.AppendLine(value); }
    public void HttpStatus(int status) { record["httpStatus"] = status; }
    public void Complete(string content) { record["response"] = content; record["status"] = "completed"; }
    public void Fail(Exception error, string partial)
    {
        record["status"] = error is OperationCanceledException ? "cancelled" : "failed";
        record["error"] = error.ToString(); record["response"] = partial;
    }
    public void Dispose()
    {
        if (!enabled) return;
        record["elapsedMilliseconds"] = clock.ElapsedMilliseconds;
        if (raw.Length > 0) record["rawResponse"] = raw.ToString();
        ResoneDailyLog.WriteBlock("LLM", $"END {label}", record.ToJsonString(new System.Text.Json.JsonSerializerOptions(ResoneJson.Default.Options) { WriteIndented = true }));
    }
}
