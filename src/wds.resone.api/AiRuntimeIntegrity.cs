namespace Wds.Resone.Api;

/// <summary>
/// Coordinates cheap normal-start validation with a full integrity pass after a local
/// inference failure. The worker writes the marker; the launcher consumes it before
/// starting the next AI stack and clears it only after provisioning/verification succeeds.
/// </summary>
public static class AiRuntimeIntegrity
{
    private const string MarkerRelativePath = "state/llm-reverify.required";

    public static string LlmReverificationMarker(string? aiRoot = null)
        => Path.Combine(Path.GetFullPath(aiRoot ?? AiRuntimeRoot.Resolve()), "state", "llm-reverify.required");

    public static bool IsLlmReverificationRequested(string? aiRoot = null)
    {
        try { return File.Exists(LlmReverificationMarker(aiRoot)); }
        catch { return false; }
    }

    public static void RequestLlmReverification(string reason, string? aiRoot = null)
    {
        try
        {
            string marker = LlmReverificationMarker(aiRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
            string safeReason = string.IsNullOrWhiteSpace(reason) ? "local inference failure" : reason.Trim();
            if (safeReason.Length > 1024) safeReason = safeReason[..1024];
            File.WriteAllText(marker, $"{DateTimeOffset.UtcNow:O}\n{safeReason}\n");
        }
        catch { /* recovery marker is best-effort and must never hide the original failure */ }
    }

    public static void ClearLlmReverification(string? aiRoot = null)
    {
        try
        {
            string marker = LlmReverificationMarker(aiRoot);
            if (File.Exists(marker)) File.Delete(marker);
        }
        catch { /* a stale marker merely causes another integrity pass next launch */ }
    }
}
