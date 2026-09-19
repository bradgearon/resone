using System.Text;
using Wds.Resone.Api.Ai;
using Wds.Resone.Api.Music;

namespace Wds.Resone.Api;

/// <summary>
/// Optional non-track-scoped song composition pass. It runs after the producer outline and before
/// any section/lane composer. The result is shared unchanged with every later track composer.
/// </summary>
public static class SongComposerDesignPass
{
    // Kept as explicit defaults so this can become per-lane configuration later without changing
    // the prompt contract or persisted song state shape.
    public const int DefaultMelodyCenterOctave = 3;
    public const int DefaultMelodyMaxOctave = 5;

    public static async Task<string> CreateAsync(
        ILocalChatModelClient model,
        string assetsRoot,
        string brief,
        string producerDesign,
        int tempo,
        string meter,
        int targetBars,
        bool useAhd,
        string existingComposerOverview,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(brief)) throw new ArgumentException("A song brief is required.", nameof(brief));
        if (string.IsNullOrWhiteSpace(producerDesign)) throw new ArgumentException("Producer notes are required.", nameof(producerDesign));

        var instructions = MusicCompositionInstructions.Load(assetsRoot);
        string tips = InstructionContent.Read(assetsRoot, "composition-tips.md");

        string system = $$"""
You are Resone's COMPOSER DESIGN PASS. You run once after the song producer and before any track-scoped composition.

This is deliberately NOT a lane/track generation request. Do not emit `track=` headers and do not attempt to arrange every instrument. Instead, design the shared musical DNA that every later lane composer will receive: emotional note material, a melody seed, reusable motifs, harmonic/chord progression material, and prepared melodic responses.

You are still Resone's music composer. Use the supplied Resone composer instructions, composition tips, Resonator notation reference, Interval Emotion Field Guide, and AHD reference as your musical/technical rules. Where the ordinary composer instructions say to generate ONLY a selected lane, that selected-lane restriction is overridden for this one design pass only. All notation you create must still be legal, directly reusable Resonator notation.

DESIGN PRINCIPLES
- Start from the MAIN FEEL of the user's song. Choose a compact emotional note palette first: tonal home plus the actual notes/interval relationships whose Core/Ascending/Descending/Phrase/Continuation/Chord/etc. properties create that feeling. Build the melody, motifs, and chords from this same emotional pitch material so they belong to one piece.
- Key choice is creative. Vary key/tonal center across songs instead of habitually defaulting to C major or A minor. Choose the key because it serves the requested feel. A modulation/key-area change may be designed when it helps the producer plan.
- Melody register default: center melodic material around octave {{DefaultMelodyCenterOctave}}. Octaves 3-4 should contain most melodic notes. Octave 5 is available for intentional peaks. NEVER write a melodic note in octave 6 or above. Lower notes may be used when the emotion needs them. These are design-pass defaults and may become lane-configurable later.
- Anchored Harmonic Divergence is always part of your compositional vocabulary. When AHD is enabled, you are explicitly allowed to use emotionally purposeful notes outside the current key. Do not "correct" a designed AHD note merely because it is chromatic; reason from tonal home, activated material, and narrative history. When AHD is disabled, keep this design conventionally tonal/chromatic without relying on AHD activation.
- Statements/answers/contrasts/continuations must use the existing composer rule: important response notes are designed relative to corresponding remembered positions in the source statement/motif, and the Interval Emotion Field Guide's Continuation property determines how the relationship feels. Rhythm/delivery should vary rather than mechanically copy the source.
- Produce at least ONE and at most THREE response variants TOTAL across Answers, Contrasts, and Continuations combined. Do NOT generate 1-3 of each family. Choose the mix that best serves the song; for example, one Answer + one Contrast, or one Continuation alone, or one of each for three total. Every variant must contain directly reusable Resonator notation and explain which seed/motif it responds to.
- Chord progressions must be actual Resonator chord notation, not chord names alone. Chords should come from/support the same emotional note palette and may use AHD color when enabled.
- Keep the design concise enough to be useful to every later lane composer. Prefer a small number of strong motifs that can survive orchestration and section development.

OUTPUT CONTRACT — plain text with EXACTLY these top-level labels:
Composer design:
Key / tonal plan: ...
Emotional note palette: ...
Melody seed:
`tempo={{tempo}} {{meter}} key=<KEY> | ... |`
Motifs:
- Motif A — role: `...`
- Motif B — role: `...`
Chord progression:
`tempo={{tempo}} {{meter}} key=<KEY> | [..] ... |`
Response variants (1-3 total):
- Answer to <seed/motif> — Continuation relationship / intended feeling: `...`
- Contrast to <seed/motif> — Continuation relationship / intended feeling: `...`
- Continuation from <seed/motif> — Continuation relationship / intended feeling: `...`
Usage notes: ...

NOTATION RULES
- Put every reusable musical fragment in backticks.
- Melody seed should normally be about 2-8 bars: enough to establish identity, not a whole song.
- Motifs should normally be short 1-2 bar cells.
- Response examples should be approximately comparable in size to the motif/statement they answer.
- Chord progression should be long enough to establish the song's harmonic identity, normally 4-8 bars.
- Use exact pitches, durations, rests, holds, chords, dynamics, and other legal Resonator syntax where musically useful.
- Do not return JSON or Markdown headings. The labels above and bullet lines are plain text; backticks exist only to delimit exact Resonator fragments for later composers.
""";

        var user = new StringBuilder()
            .AppendLine("ORIGINAL SONG REQUEST")
            .AppendLine(brief.Trim()).AppendLine()
            .AppendLine("PRODUCER OUTLINE")
            .AppendLine(producerDesign.Trim()).AppendLine()
            .AppendLine("SONG FORM")
            .AppendLine($"Tempo: {tempo}")
            .AppendLine($"Meter: {meter}")
            .AppendLine($"Approximate total bars: {targetBars}")
            .AppendLine($"AHD: {(useAhd ? "ENABLED — purposeful out-of-key/activated material is allowed" : "disabled")}")
            .AppendLine();

        if (!string.IsNullOrWhiteSpace(existingComposerOverview))
        {
            user.AppendLine("EXISTING SONG COMPOSER OVERVIEW")
                .AppendLine("This is the established musical DNA from the song being modified. Keep its successful tonal plan, emotional note palette, motifs, chord relationships, and response logic unless the new request explicitly changes them. Produce an updated composer design that remains recognizably the same piece.")
                .AppendLine(existingComposerOverview.Length > 24000 ? existingComposerOverview[..24000] : existingComposerOverview)
                .AppendLine();
        }

        user.AppendLine("RESone COMPOSER CORE INSTRUCTIONS")
            .AppendLine(instructions.SystemPrompt)
            .AppendLine()
            .AppendLine("RESone COMPOSER REALIZATION / RESPONSE RULES")
            .AppendLine(instructions.ArrangementInstructions)
            .AppendLine()
            .AppendLine("COMPOSITION TIPS")
            .AppendLine(tips)
            .AppendLine()
            .AppendLine("COMPOSER REFERENCES")
            .AppendLine(instructions.References)
            .ToString();

        string design = (await model.CompleteTextStreamingAsync(
            [new ChatMessage("system", system), new ChatMessage("user", user.ToString())],
            "SongComposerDesign",
            null,
            token).ConfigureAwait(false)).Trim();

        if (string.IsNullOrWhiteSpace(design))
            throw new InvalidDataException("The composer design pass returned empty notes.");

        // This packet is prompt context, not a generated track. Keep enough room for several exact
        // notation fragments while protecting every later section request from accidental prompt bloat.
        return design.Length > 24000 ? design[..24000] : design;
    }
}
