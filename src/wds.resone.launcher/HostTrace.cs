using Wds.Resone.Api;
using Wds.Resone.Api.Ai;

namespace Wds.Resone.Launcher;

/// <summary>Traces requests before validation or model preparation can fail.</summary>
internal sealed class HostTrace
{
    private readonly bool enabled;
    public HostTrace(ResoneSettings settings) => enabled = settings.LogLlmRequests;
    public void Write(string stage, string id = "", string detail = "")
    {
        if (!enabled) return;
        ResoneDailyLog.Write("HOST", $"stage={stage}; requestId={id}; {detail}");
    }
}
