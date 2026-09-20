using System.Text;
using System.Text.Json.Nodes;
using Wds.Resone.Api.Ai;

namespace Wds.Resone.Api;

/// <summary>
/// Optional, lane-scoped best-effort post-processing pass. It receives only the notation for the
/// chunk that was just composed plus the lane's persisted free-form instructions. A failure here
/// must never invalidate otherwise usable Composer output.
/// </summary>
public sealed class MusicPostProcessor(string assetsRoot)
{
    public async Task ApplyAsync(
        ILocalChatModelClient client,
        JsonObject composerResult,
        Lane target,
        int tempo,
        string meter,
        int bars,
        bool useAhd,
        bool songMode,
        CancellationToken token,
        Func<string, Task>? progress = null)
    {
        if (!target.PostProcessingEnabled)
            return;

        string instructions = ActiveInstructions(target);
        if (string.IsNullOrWhiteSpace(instructions))
            return;

        JsonObject? originalTrack = FindTrack(composerResult, target.Id);
        string originalNotation = originalTrack?["notation"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(originalNotation))
        {
            AddWarning(composerResult, $"Post processing was enabled for {target.Name}, but the Composer returned no notation to post-process; kept the Composer MIDI unchanged.");
            return;
        }

        try
        {
            if (progress is not null)
            {
                bool registerWork = LooksLikeRegisterWork(instructions);
                string message = registerWork
                    ? (songMode ? "Shifting octaves…" : "Composing · Shifting octaves…")
                    : (songMode ? "Post processing…" : "Composing · Post processing…");
                await progress(message).ConfigureAwait(false);
            }

            string intervalGuide = InstructionContent.Read(assetsRoot, "interval_emotion_field_guide.md");
            string ahdGuide = InstructionContent.Read(assetsRoot, "anchored_harmonic_divergence.md");
            string notationReference = InstructionContent.Read(assetsRoot, "resonator_api_v0.1.md");

            string system = $"""
You are the post processing for music composed by Resone. Using the information from the composer, make the notes selected meet these requirements:

{instructions.Trim()}
""";

            var user = new StringBuilder()
                .AppendLine("CURRENT COMPOSER CHUNK")
                .AppendLine(originalNotation.Trim())
                .AppendLine()
                .AppendLine("RESONATOR NOTATION REFERENCE")
                .AppendLine(notationReference)
                .AppendLine()
                .AppendLine("INTERVAL EMOTION FIELD GUIDE")
                .AppendLine(intervalGuide)
                .AppendLine()
                .AppendLine("ANCHORED HARMONIC DIVERGENCE REFERENCE")
                .AppendLine(ahdGuide);

            string text = await client.CompleteTextStreamingAsync(
                [new ChatMessage("system", system), new ChatMessage("user", user.ToString())],
                songMode ? "SongChunkPostProcessing" : "MusicPostProcessing",
                null,
                token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            // Decode against one lane only. The ordinary decoder already salvages malformed tokens
            // and missing/wrong track wrappers. allowEmptyTracks keeps this pass fail-soft.
            var oneLane = new SongProject
            {
                Tempo = tempo,
                Meter = meter,
                Bars = bars,
                Lanes = [target]
            };
            string originalBrief = originalTrack?["originalBrief"]?.GetValue<string>() ?? target.OriginalBrief;
            JsonObject processed = ArrangementComposer.Decode(text, oneLane, originalBrief, allowEmptyTracks: true);
            JsonObject? processedTrack = FindTrack(processed, target.Id);
            if (processedTrack is not null && originalTrack?["originalBrief"] is JsonNode originalBriefNode)
                processedTrack["originalBrief"] = originalBriefNode.DeepClone();
            int playable = (processedTrack?["notes"] as JsonArray)?.Count ?? 0;
            if (processedTrack is null || playable == 0)
            {
                AddWarning(composerResult, $"Post processing for {target.Name} returned no usable MIDI; kept the original Composer chunk.");
                CopyWarnings(processed, composerResult, "Post processing: ");
                return;
            }

            ReplaceTrack(composerResult, target.Id, processedTrack);
            CopyWarnings(processed, composerResult, "Post processing: ");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Post processing is optional enhancement. Never let it destroy or stop otherwise valid music.
            AddWarning(composerResult, $"Post processing for {target.Name} could not be applied ({ex.Message}); kept the original Composer chunk.");
        }
    }

    private static string ActiveInstructions(Lane lane)
    {
        if (lane.PostProcessingDeepMode && !string.IsNullOrWhiteSpace(lane.PostProcessingDeepInstructions))
            return lane.PostProcessingDeepInstructions;
        return lane.PostProcessingInstructions;
    }

    private static bool LooksLikeRegisterWork(string value)
        => value.Contains("octave", StringComparison.OrdinalIgnoreCase)
           || value.Contains("register", StringComparison.OrdinalIgnoreCase)
           || value.Contains("above ", StringComparison.OrdinalIgnoreCase)
           || value.Contains("below ", StringComparison.OrdinalIgnoreCase);

    private static JsonObject? FindTrack(JsonObject result, string laneId)
    {
        if (result["tracks"] is not JsonArray tracks) return null;
        foreach (var node in tracks)
            if (node is JsonObject track && string.Equals(track["laneId"]?.GetValue<string>(), laneId, StringComparison.Ordinal))
                return track;
        return tracks.Count == 1 ? tracks[0] as JsonObject : null;
    }

    private static void ReplaceTrack(JsonObject result, string laneId, JsonObject replacement)
    {
        if (result["tracks"] is not JsonArray tracks) return;
        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i] is JsonObject track && string.Equals(track["laneId"]?.GetValue<string>(), laneId, StringComparison.Ordinal))
            {
                tracks[i] = replacement.DeepClone();
                return;
            }
        }
        if (tracks.Count == 1) tracks[0] = replacement.DeepClone();
    }

    private static void AddWarning(JsonObject result, string warning)
    {
        var warnings = result["warnings"] as JsonArray;
        if (warnings is null)
        {
            warnings = [];
            result["warnings"] = warnings;
        }
        warnings.Add((JsonNode?)JsonValue.Create(warning));
    }

    private static void CopyWarnings(JsonObject source, JsonObject target, string prefix)
    {
        if (source["warnings"] is not JsonArray warnings) return;
        foreach (var warning in warnings)
        {
            string? text = warning?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(text)) AddWarning(target, prefix + text);
        }
    }
}
