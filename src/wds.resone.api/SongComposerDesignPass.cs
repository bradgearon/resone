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

    public static Task<string> CreateAsync(
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
        => CreateAsync(model, assetsRoot, brief, producerDesign, tempo, meter, targetBars, useAhd, existingComposerOverview, "", token);

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
        string genreContext,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(brief)) throw new ArgumentException("A song brief is required.", nameof(brief));
        if (string.IsNullOrWhiteSpace(producerDesign)) throw new ArgumentException("Producer notes are required.", nameof(producerDesign));

        var instructions = MusicCompositionInstructions.Load(assetsRoot);
        string tips = InstructionContent.Read(assetsRoot, "composition-tips.md");

        string system = """
You are Resone's composer design pass. You run once after the song producer and before any track-scoped composition.

Design the shared musical DNA that every later lane composer will receive: emotional note material, a melody seed, reusable motifs, harmonic/chord progression material, and prepared melodic responses.

Use the supplied composer instructions, composition tips, Resonator notation reference, Interval Emotion Field Guide, and AHD reference as your musical/technical rules.

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
- Put every reusable musical fragment in backticks.
- Melody seed should normally be about 2-8 bars: enough to establish identity, not a whole song.
- Motifs should normally be short 1-2 bar cells.
- Response examples should be approximately comparable in size to the motif/statement they answer.
- Chord progression should be long enough to establish the song's harmonic identity, normally 4-8 bars.
- Use exact pitches, durations, rests, holds, chords, dynamics, and other legal Resonator syntax where musically useful.
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

        if (!string.IsNullOrWhiteSpace(genreContext))
        {
            user.AppendLine("RETRIEVED GENRE GUIDANCE")
                .AppendLine("Use the selected genre brief to shape development, phrase behavior, harmony, rhythmic identity, density, transitions, and climax behavior. The user's request and established song identity remain authoritative.")
                .AppendLine(genreContext)
                .AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(existingComposerOverview))
        {
            user.AppendLine("EXISTING SONG COMPOSER OVERVIEW")
                .AppendLine("This is the established musical DNA from the song being modified. Keep its successful tonal plan, emotional note palette, motifs, chord relationships, and response logic unless the new request explicitly changes them. Produce an updated composer design that remains recognizably the same piece.")
                .AppendLine(existingComposerOverview)
                .AppendLine();
        }

        user.AppendLine("COMPOSITION TIPS")
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
        return design;
    }
}
