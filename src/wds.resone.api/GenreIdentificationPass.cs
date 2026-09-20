using System.Text.RegularExpressions;
using Wds.Resone.Api.Ai;

namespace Wds.Resone.Api;

/// <summary>
/// Tiny fail-soft classifier used only for ordinary (non-Song-mode) drum-lane requests.
/// It identifies the requested genre/subgenre; deterministic local retrieval then supplies the actual guide text.
/// </summary>
public static class GenreIdentificationPass
{
    public static async Task<string> IdentifyAsync(ILocalChatModelClient model, string userRequest, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(userRequest)) return "";
        var messages = new List<ChatMessage>
        {
            new("system", "Identify the musical genre or subgenre requested or strongly implied by the user. Return ONLY one short genre/subgenre string, for example `uplifting trance`, `jungle`, `progressive metal`, or `classical`. If no genre can reasonably be identified, return exactly NONE. Do not explain, list alternatives, or describe mood/instruments."),
            new("user", userRequest.Trim())
        };

        try
        {
            string raw = (await model.CompleteTextStreamingAsync(messages, "DrumGenreIdentification", null, token).ConfigureAwait(false)).Trim();
            string line = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
            line = Regex.Replace(line, @"^(?:genre|subgenre)\s*:\s*", "", RegexOptions.IgnoreCase).Trim().Trim('`', '"', '\'', '*', '-', ' ');
            return Regex.IsMatch(line, @"^(?:none|unknown|no genre|not specified)\.?$", RegexOptions.IgnoreCase) ? "" : line;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // Genre classification enriches a drum request; it must never make composition fail.
            return "";
        }
    }
}
