using System.Text;
using Wds.Resone.Api.Ai;

namespace Wds.Resone.Api;

/// <summary>First full-song pass: plain-text producer notes only. It never emits notes or MIDI.</summary>
public static class SongCompositionDesigner
{
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
Song title: ...
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
Purpose: ...
Feeling / energy: ...
Development: ...
Transition: ...
Final payoff: ...

IMPORTANT SECTION FORMAT CONTRACT:
- EVERY section header must literally begin with the word `SECTION`, then its sequential integer, then a stable id in square brackets, then `— title`.
- Valid: `SECTION 1 [intro] — Overture Build`
- Invalid: `[INTRO] — Overture Build`
- Invalid: `1. Intro`
- Invalid: `Intro:`
- Put `Bars: N` on the very next line after every SECTION header.
- Put Purpose, Feeling / energy, Development, and Transition on separate lines.
- Before returning, silently verify that at least one literal `SECTION N [id] — title` line exists and that every section has a `Bars:` line.

Rules:
- Song title is a short evocative title (normally 2–6 words) for this specific song. Do not quote it and do not reuse the user's request verbatim unless it already reads like a title.
- NEVER use placeholder titles such as `Untitled Song`, `Untitled`, `New Song`, or just `Song`.
- Overall identity includes the genre/subgenre when one is implied or requested, plus the defining sonic identity.
- Motif strategy says what should recur, answer, counter, transform, be withheld, or return. Describe ideas; do not invent literal notes. When call/response matters, distinguish an answer that develops the statement from a counter that deliberately contrasts or reframes it; later composers will realize both from remembered note positions using Continuation relationships.
- Rhythmic strategy defines the pulse/groove development at a useful high level, including useful variation in the delivery of answers/counters rather than literal rhythmic copying.
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
            "SongDesign",
            null,
            token).ConfigureAwait(false)).Trim();
        if (string.IsNullOrWhiteSpace(design)) throw new InvalidDataException("The song producer returned empty notes.");
        return design.Length > 24000 ? design[..24000] : design;
    }

    public static string ExtractTitle(string design, string fallbackBrief)
    {
        // Local models sometimes add Markdown even though the producer asks for
        // plain text. Accept bullets/bold around the label so a harmless formatting
        // variation cannot silently turn the saved song into "Untitled Song".
        var match = System.Text.RegularExpressions.Regex.Match(
            design ?? "",
            @"(?im)^\s*(?:[-*]\s*)?(?:\*{1,2})?Song\s+title(?:\*{1,2})?\s*:\s*(?:\*{1,2})?(?<title>[^\r\n]+)");
        string title = match.Success ? CleanTitle(match.Groups["title"].Value) : "";
        if (IsGenericTitle(title)) title = "";
        if (string.IsNullOrWhiteSpace(title)) title = FallbackTitle(fallbackBrief);
        if (string.IsNullOrWhiteSpace(title)) title = "Untitled Song";
        return title.Length > 120 ? title[..120].Trim() : title;
    }

    private static string CleanTitle(string value)
    {
        string title = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
        title = title.Trim('"', '\'', '“', '”', '*', '_', '`', '-', '–', '—', ':').Trim();
        return title;
    }

    private static bool IsGenericTitle(string value)
    {
        string normalized = CleanTitle(value).ToLowerInvariant();
        return normalized is "" or "untitled" or "untitled song" or "new song" or "song" or "new composition";
    }

    private static string FallbackTitle(string brief)
    {
        string value = string.Join(' ', (brief ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(value)) return "";

        // Remove common command scaffolding before using the request as a safe
        // provisional title. The producer title should normally win; this exists
        // only so persistence never falls back to a generic placeholder.
        value = System.Text.RegularExpressions.Regex.Replace(
            value,
            @"(?i)^(?:please\s+)?(?:make|create|write|compose|generate)\s+(?:me\s+)?(?:a|an|the)?\s*",
            "").Trim();
        string[] words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 7) value = string.Join(' ', words.Take(7));
        if (value.Length > 64) value = value[..64].TrimEnd() + "…";
        return value;
    }
}
