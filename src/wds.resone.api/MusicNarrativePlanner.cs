using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wds.Resone.Api.Ai;

namespace Wds.Resone.Api.Music;

/// <summary>
/// Silent pre-composition pass that translates a user's emotional request into an ordered
/// interval/perspective narrative plan. The result is the composer roadmap, never notation.
/// </summary>
public static class MusicNarrativePlanner
{
    private const int MaxNarrativeTokens = 4096;

    public static async Task<string> CreateAsync(
        ILocalChatModelClient model,
        string userRequest,
        string intervalEmotionGuide,
        bool useAhd,
        int bars,
        int tempo,
        string meter,
        string existingNotation,
        string originalBrief,
        IReadOnlyList<string> promptHistory,
        double existingLengthBeats,
        string songGenerationContext,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(userRequest))
            throw new ArgumentException("A musical request is required.", nameof(userRequest));
        if (string.IsNullOrWhiteSpace(intervalEmotionGuide))
            throw new InvalidOperationException("The interval emotion field guide is required for narrative planning.");
        if (bars < 1)
            throw new ArgumentOutOfRangeException(nameof(bars));
        if (tempo < 1)
            throw new ArgumentOutOfRangeException(nameof(tempo));
        if (string.IsNullOrWhiteSpace(meter))
            throw new ArgumentException("A meter is required for narrative planning.", nameof(meter));

        bool isRevision = !string.IsNullOrWhiteSpace(existingNotation);
        string ahd = useAhd
            ? "Anchored Harmonic Divergence (AHD) is enabled. Treat chromatic or divergent interval colors as valid expressive material; do not reject an emotional gesture merely because conventional tonal analysis might call it outside the key."
            : "Anchored Harmonic Divergence (AHD) is disabled. The narrative still describes emotional interval gestures, but the later composer will keep them within the requested tonal context.";

        string system = """
You are Resone's silent musical narrative planner. You do NOT compose notes, chords, MIDI, or Resonator notation. Before the music composer runs, convert the user's request into the actual compositional plan that the later music generator must follow.

Use the Interval Emotion Field Guide as a functional vocabulary, not merely as descriptive inspiration. Use its perspectives exactly when useful: Core / home, Ascending, Descending, Phrase, Continuation, Strong beat, Weak beat, Held, Repeated, Chord, Bass motion, Register, and Resolution tendency.

Return plain text only. Build an ordered emotional journey. The plan must be mechanically actionable. For each phrase/section include ALL of the following:
- PHRASE N — bars X-Y (or the closest practical beat/bar span for the requested length).
- Feeling: the specific emotion, feeling, image, bodily sense, or descriptive state being created. Preserve rich descriptive language rather than reducing everything to broad labels such as happy/sad/tense.
- Emotional transition: what this phrase grows out of and what it changes into.
- Required interval gestures: list every interval + perspective that MUST occur recognizably in this phrase. If the feeling requires multiple intervals, explicitly list all of them and explain whether they work together, in sequence, or as setup/payoff.
- Phrasing role: how the phrase speaks (opening statement, question, answer, expansion, withheld idea, breakthrough, return, resting point, etc.) and which Phrase perspective supports it.
- Continuation: what emotional/musical thought comes next, and which interval + Continuation perspective creates that handoff. The final phrase should instead describe the intended resting/closure behavior.
- Placement / emphasis: where the required gesture should matter (strong beat, weak beat, held arrival, repetition, chord, bass motion, register opening, resolution, etc.).
- Payoff / memory: what expectation, note-color, interval, motif, register, or emotional idea is established, withheld, transformed, recalled, or finally fulfilled.

A REQUIRED INTERVAL GESTURE IS A REAL COMPOSITIONAL REQUIREMENT. The later composer must actually place that interval relationship in the generated notes in the named perspective. Do not use an emotional adjective as a substitute for the interval. Do not claim that merely moving higher in register satisfies an ascending major 6th, or that a bright chord substitutes for a required major 3rd melodic gesture.

For AHD material, reason across multiple simultaneous perspectives when useful:
1. Tonal-home interval: what a note/gesture means relative to the established tonal home.
2. Activated-material interval: what it means relative to an earlier emphasized, divergent, or emotionally activated pitch/material.
3. Narrative-history perspective: how repetition, withholding, delayed return, octave/register transfer, duration, and prior exposure change the emotional meaning of that note or interval.
When these perspectives conflict, that conflict can be the point. A pitch may be dissonant against the tonal home but expansive, secure, tender, heroic, or peaceful relative to activated material. Do not call such an AHD color wrong or out of tune. Describe the intended combined feeling.

Use held notes, repeated notes, chord intervals, bass motion, register, and resolution perspective when they materially strengthen the user's requested emotion. An emotion may require more than one interval requirement; say so explicitly rather than forcing one interval to carry the entire feeling.

Prefer a developing emotional story over repeating one adjective. Preserve the user's intent exactly. If the user asks for happiness, for example, the plan may develop through curiosity, optimism, warmth, playfulness, adventure, affection, freedom, confidence, triumph, tenderness, and contentment—but only choose stages that fit the actual request.

If CURRENT MATERIAL / UPDATE CONTEXT is supplied, this is a revision request, not a blank composition. Read the existing notation as the current musical structure and use the original brief/prompt history as musical memory. Extract whatever useful ideas already exist, then plan the requested change while preserving material the user did not ask to replace. For short update requests such as "More energy", "dreamier", "simplify", or "add variation", describe how the existing phrases/narrative should change rather than inventing an unrelated song from scratch. Required interval gestures should be changes/additions that serve the update, while established motifs, timing, AHD relationships, and successful emotional payoffs should survive unless the request conflicts with them.

Never plan beyond the requested bar count. Every PHRASE bar range must fit inside bars 1 through the requested final bar; closure must occur within that form.

Keep the plan proportional to the requested duration: usually 4-10 phrases/sections. End with:
Overall arc: feeling -> feeling -> feeling ...
Required-gesture summary: a compact checklist of the interval/perspective gestures the composer must realize, in order.
""";

        var user = new StringBuilder()
            .AppendLine("ORIGINAL USER MUSIC REQUEST")
            .AppendLine(userRequest.Trim())
            .AppendLine()
            .AppendLine("REQUESTED FORM")
            .AppendLine($"Tempo: {tempo}")
            .AppendLine($"Meter: {meter}")
            .AppendLine($"Bars: {bars}")
            .AppendLine()
            .AppendLine("HARMONIC MODE")
            .AppendLine(ahd)
            .AppendLine();

        if (isRevision)
        {
            user.AppendLine("CURRENT MATERIAL / UPDATE CONTEXT")
                .AppendLine("This is an update to existing music. Preserve what the new request does not ask to replace.")
                .AppendLine($"Existing clip length: {existingLengthBeats:0.###} quarter-note beats")
                .AppendLine("Original brief: " + (string.IsNullOrWhiteSpace(originalBrief) ? "(none)" : originalBrief.Trim()))
                .AppendLine("Previous update requests: " + (promptHistory.Count == 0 ? "(none)" : string.Join(" -> ", promptHistory)))
                .AppendLine("Existing Resonator notation / timing:")
                .AppendLine(existingNotation.Length > 16000 ? existingNotation[..16000] : existingNotation)
                .AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(songGenerationContext))
        {
            user.AppendLine("FULL SONG CONTEXT")
                .AppendLine("The following context comes from Resone's deterministic song-generation provisioner. Use it to keep this section consistent with the larger piece. Do not rewrite the global song design; plan the selected lane/chunk so it fulfills the assigned section and preserves exact musical memories when they are called for.")
                .AppendLine(songGenerationContext.Length > 24000 ? songGenerationContext[..24000] : songGenerationContext)
                .AppendLine();
        }

        user.AppendLine("INTERVAL EMOTION FIELD GUIDE")
            .AppendLine(intervalEmotionGuide)
            .ToString();

        var messages = new List<ChatMessage>
        {
            new("system", system),
            new("user", user.ToString())
        };

        string narrative = (await model.CompleteTextStreamingAsync(
            messages,
            MaxNarrativeTokens,
            "MusicNarrative",
            null,
            token).ConfigureAwait(false)).Trim();

        if (string.IsNullOrWhiteSpace(narrative))
            throw new InvalidDataException("The music narrative planner returned an empty response.");
        if (narrative.Length > 24000)
            narrative = narrative[..24000];
        return narrative;
    }

    public static string AppendNarrativePlan(string musicRequest, string narrative)
    {
        return musicRequest
            + "\n\nMUSICAL NARRATIVE PLAN — FOLLOW THIS PLAN\n"
            + "This plan was generated silently from the same original user request using the Interval Emotion Field Guide. "
            + "The original user request remains authoritative if there is a direct conflict, but otherwise this is the compositional plan for the music and must be executed, not treated as optional inspiration. "
            + "Follow the emotional order, phrase/section placement, phrasing roles, continuation targets, payoff/memory instructions, and every Required interval gesture. "
            + "A required interval/perspective must occur recognizably in the actual notes in that role; do not substitute a register change, generic mood, chord quality, or unrelated interval for it.\n"
            + narrative;
    }
}
