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
    public static async Task<string> CreateAsync(
        ILocalChatModelClient model,
        string userRequest,
        string intervalEmotionGuide,
        string resonatorNotationReference,
        bool useAhd,
        int bars,
        int tempo,
        string meter,
        string existingNotation,
        string originalBrief,
        IReadOnlyList<string> promptHistory,
        double existingLengthBeats,
        string songGenerationContext,
        string composerOverview,
        string laneGenreContext,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(userRequest))
            throw new ArgumentException("A musical request is required.", nameof(userRequest));
        if (string.IsNullOrWhiteSpace(intervalEmotionGuide))
            throw new InvalidOperationException("The interval emotion field guide is required for narrative planning.");
        if (string.IsNullOrWhiteSpace(songGenerationContext) && string.IsNullOrWhiteSpace(resonatorNotationReference))
            throw new InvalidOperationException("The Resonator notation reference is required for non-song director planning.");
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

        string system = string.IsNullOrWhiteSpace(songGenerationContext)
            ? """
You are Resone's silent musical director. You run before the arrangement composer.

Turn the user's request into a clear compositional plan for the selected lane. For short requests develop something impactual and relevant but be creative.

Use the Interval Emotion Field Guide as your musical vocabulary.

DESIGN PRINCIPLES
- Build chords and motifs around the impactual notes from intervals used in the piece.
- Vary key/tonal center across songs.
- Melody register default: center melodic material around octave 3. Octaves 3-4 should contain most melodic notes. Octave 5 is available for intentional peaks.
- Anchored Harmonic Divergence is always part of your compositional vocabulary. When AHD is enabled, you can use it to create much richer music.
- Produce 1-3 response variants across Answers, Contrasts, and Continuations combined.
- Chord progressions must be actual Resonator chord notation, not chord names. Chords should come from/support the same emotional note palette and may use AHD color when enabled.

OUTPUT CONTRACT — plain text with EXACTLY these top-level labels:
Composer design:
Key / tonal plan: ...
Emotional note palette: ...
Melody seed:
`tempo=<TEMPO> <METER> key=<KEY> | ... |`
Motifs:
- Motif A — role: `...`
- Motif B — role: `...`
Chord progression:
`tempo=<TEMPO> <METER> key=<KEY> | [..] ... |`
Response variants (1-3 total):
- Answer to <seed/motif> — Continuation relationship / intended feeling: `...`
- Contrast to <seed/motif> — Continuation relationship / intended feeling: `...`
- Continuation from <seed/motif> — Continuation relationship / intended feeling: `...`
Usage notes: ...

NOTATION RULES
- All notation must be in Resonator syntax.
- Each note for chords must be listed, do not speak of chords by name.
- Put every reusable musical fragment in backticks.
- Melody seed should normally be about 2-8 bars: enough to establish identity, not a whole song.
- Motifs should normally be short 1-2 bar cells.
- Response examples should be approximately comparable in size to the motif/statement they answer.
- Chord progression should be long enough to establish the song's harmonic identity, normally 4-8 bars.
- Use exact pitches, durations, rests, holds, chords, dynamics, and other legal Resonator syntax where musically useful.
"""
            : """
You are Resone's silent musical narrative planner. You do NOT compose notes, chords, MIDI, or Resonator notation. Before the music composer runs, convert the user's request into the actual compositional plan that the later music generator must follow.

Use the Interval Emotion Field Guide as a functional vocabulary, not merely as descriptive inspiration. Use its perspectives exactly when useful: Core / home, Ascending, Descending, Phrase, Continuation, Strong beat, Weak beat, Held, Repeated, Chord, Bass motion, Register, and Resolution tendency.

Return plain text only. Build an ordered emotional journey. The plan must be mechanically actionable. For each phrase/section include ALL of the following:
- PHRASE N — bars X-Y (or the closest practical beat/bar span for the requested length).
- Feeling: the specific emotion, feeling, image, bodily sense, or descriptive state being created. Preserve rich descriptive language rather than reducing everything to broad labels such as happy/sad/tense.
- Emotional transition: what this phrase grows out of and what it changes into.
- Required interval gestures: list every interval + perspective that MUST occur recognizably in this phrase. If the feeling requires multiple intervals, explicitly list all of them and explain whether they work together, in sequence, or as setup/payoff.
- Phrasing role: how the phrase speaks (opening statement, question, answer, counter, expansion, withheld idea, breakthrough, return, resting point, etc.) and which Phrase perspective supports it.
- Continuation: what emotional/musical thought comes next, and which interval + Continuation perspective creates that handoff. The final phrase should instead describe the intended resting/closure behavior.
- Placement / emphasis: where the required gesture should matter (strong beat, weak beat, held arrival, repetition, chord, bass motion, register opening, resolution, etc.).
- Payoff / memory: what expectation, note-color, interval, motif, register, or emotional idea is established, withheld, transformed, recalled, or finally fulfilled. If a phrase creates a setup that must be completed by a later composer/section/lane, say exactly what must be fulfilled so it can become a composer handoff commitment.

STATEMENT / ANSWER / COUNTER PLANNING:
- A statement establishes remembered positional note anchors.
- An answer should carry the statement farther: design its important notes relative to the source notes at corresponding remembered positions and choose the interval relationship from the field guide's Continuation perspective.
- A counter uses the same remembered-position + Continuation logic but deliberately contrasts, rejects, interrupts, inverts, or reframes the previous statement rather than simply agreeing with it.
- Rhythm and delivery are part of the meaning. Do not require literal rhythmic copying. A three-hit repeated statement may receive continuation-related responses with changed spacing, staccato/held treatment, syncopation, acceleration, or a delayed third response.
- When CURRENT SONG SECTION SCOPE contains OPEN COMPOSER COMMITMENTS, plan to fulfill any item that applies to the current section/lane and preserve later-lane/later-section obligations rather than treating them as optional history.

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
                .AppendLine(existingNotation)
                .AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(songGenerationContext))
        {
            user.AppendLine("CURRENT SONG SECTION SCOPE")
                .AppendLine("This is the only song section you are planning. Use shared musical DNA and prior memories only as context; do not create phrase/bar plans for any other section.")
                .AppendLine(songGenerationContext)
                .AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(laneGenreContext))
        {
            user.AppendLine("LANE GENRE GUIDANCE")
                .AppendLine("Use this compact retrieved grammar for the current lane. The original user request and existing material remain authoritative.")
                .AppendLine(laneGenreContext)
                .AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(composerOverview))
        {
            user.AppendLine("EXISTING SONG COMPOSER OVERVIEW")
                .AppendLine("This is persistent musical DNA from the song's original composer design pass. Use it when planning this revision. Preserve established tonal centers, emotional note palette, motifs, chord relationships, AHD activations, and designed statement/answer/contrast/continuation behavior unless the user's new request explicitly changes them.")
                .AppendLine(composerOverview)
                .AppendLine();
        }

        user.AppendLine("INTERVAL EMOTION FIELD GUIDE")
            .AppendLine(intervalEmotionGuide);

        if (string.IsNullOrWhiteSpace(songGenerationContext))
        {
            user.AppendLine()
                .AppendLine("RESONATOR NOTATION REFERENCE")
                .AppendLine(resonatorNotationReference);
        }

        var messages = new List<ChatMessage>
        {
            new("system", system),
            new("user", user.ToString())
        };

        string narrative = (await model.CompleteTextStreamingAsync(
            messages,
            "MusicNarrative",
            null,
            token).ConfigureAwait(false)).Trim();

        if (string.IsNullOrWhiteSpace(narrative))
            throw new InvalidDataException("The music narrative planner returned an empty response.");
        return narrative;
    }

    public static string AppendNarrativePlan(string musicRequest, string narrative)
    {
        return musicRequest
            + "\n\nMUSICAL NARRATIVE PLAN\n"
            + "Use the material as suggestions and work to use, but develop material for the request.\n\n"
            + narrative;
    }
}
