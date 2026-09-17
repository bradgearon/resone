using System.Text;
using Wds.Resone.Api.Ai;

namespace Wds.Resone.Api;

/// <summary>First full-song pass: plain-text producer notes only. It never emits notes or MIDI.</summary>
public static class SongCompositionDesigner
{
    private const int MaxTokens = 4096;

    public static async Task<string> CreateAsync(ILocalChatModelClient model, string brief, int tempo, string meter, int targetBars, bool useAhd, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(brief)) throw new ArgumentException("A song brief is required.", nameof(brief));
        if (tempo is < 30 or > 240) throw new ArgumentOutOfRangeException(nameof(tempo));
        if (targetBars is < 1 or > 512) throw new ArgumentOutOfRangeException(nameof(targetBars));
        if (string.IsNullOrWhiteSpace(meter)) throw new ArgumentException("Meter is required.", nameof(meter));

        string system = """
You are Resone's song producer. You run ONCE before full-song generation begins. You do NOT compose notes, chords, MIDI, JSON, or Resonator notation. Return plain text only.

Turn the user's request into a concise long-form production plan. Later, Resone will generate the requested sections one at a time and, inside each section, generate the selected lanes one at a time through its normal director and composer. Those later calls need a simple ordered section plan and a few global decisions, not a second composition essay.

Use exactly these top-level labels and no additional top-level categories:
Producer notes:
Overall identity: ...
Motif strategy: ...
Rhythmic strategy: ...
Sections:
SECTION 1 [short-id] — section title
Bars: N
Purpose: ...
Feeling / energy: ...
Development: ...
Transition: ...
SECTION 2 [short-id] — section title
Bars: N
...
Final payoff: ...

Rules:
- Overall identity includes the genre/subgenre when one is implied or requested, plus the defining sonic identity.
- Motif strategy says what should recur, answer, transform, be withheld, or return. Describe ideas; do not invent literal notes.
- Rhythmic strategy defines the pulse/groove development at a useful high level.
- Sections must be an ordered list. Give every section a stable id in square brackets and an integer Bars value.
- Normally use practical 4-32 bar sections, but honor shorter requested forms when necessary.
- The section bar counts should add up as closely as possible to the requested total length.
- Put harmonic, orchestration, lane-role, emotional, and transition details inside the relevant section's Purpose/Feeling/Development/Transition lines instead of creating more top-level headings.
- Final payoff explains the earned final return/resolution in a few sentences.
- Use a small number of memorable ideas and develop them deliberately.
- Do not write detailed notation. The later director/composer will invent the exact notes.
""";

        var user = new StringBuilder()
            .AppendLine("SONG REQUEST").AppendLine(brief.Trim()).AppendLine()
            .AppendLine("TARGET FORM")
            .AppendLine($"Tempo: {tempo}")
            .AppendLine($"Meter: {meter}")
            .AppendLine($"Approximate total length: {targetBars} bars")
            .AppendLine($"Anchored Harmonic Divergence: {(useAhd ? "enabled" : "disabled")}")
            .ToString();

        string design = (await model.CompleteTextStreamingAsync(
            [new ChatMessage("system", system), new ChatMessage("user", user)],
            MaxTokens,
            "SongDesign",
            null,
            token).ConfigureAwait(false)).Trim();
        if (string.IsNullOrWhiteSpace(design)) throw new InvalidDataException("The song producer returned empty notes.");
        return design.Length > 24000 ? design[..24000] : design;
    }
}
