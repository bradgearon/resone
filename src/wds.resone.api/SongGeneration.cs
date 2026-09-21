using System.Text;
using System.Text.RegularExpressions;

namespace Wds.Resone.Api;

/// <summary>
/// Persistent state for full-song generation. The producer runs once; later section/lane jobs only consume
/// the ordered producer plan plus compact exact musical memories returned by completed composer calls.
/// Normal lane composition does not use this type.
/// </summary>
public sealed class SongGenerationState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Brief { get; set; } = "";
    public string Title { get; set; } = "Untitled Song";
    public string Design { get; set; } = "";
    /// <summary>Optional shared non-track composer design: melody/motifs/chords/responses in Resonator notation.</summary>
    public string ComposerDesign { get; set; } = "";
    public string Summary { get; set; } = ""; // retained for wire/backward compatibility; song mode no longer requires a prose roll-up.
    public int Tempo { get; set; } = 120;
    public string Meter { get; set; } = "4/4";
    public int TargetBars { get; set; } = 64;
    public int CurrentSectionIndex { get; set; }
    public List<SongSectionMemory> Sections { get; set; } = [];
    public List<SongMusicalMemory> MusicalMemories { get; set; } = [];
    /// <summary>Open setup/payoff obligations that every later composer must either fulfill or carry forward.</summary>
    public string PendingComposerNotes { get; set; } = "";
    /// <summary>Canonical genre selected from the producer's Overall identity using the local genre-guide matcher.</summary>
    public string GenreId { get; set; } = "";
    public string GenreParentId { get; set; } = "";
    public string GenreSecondaryId { get; set; } = "";
    public string GenreSongModifierId { get; set; } = "";
    public string GenreDrumId { get; set; } = "";
    public string GenreDrumFamilyIds { get; set; } = "";
    public string GenreDrumModifierId { get; set; } = "";
    /// <summary>Legacy combined genre context retained for loading older song-generation state.</summary>
    public string GenreContext { get; set; } = "";
    /// <summary>Song-design grammar shared by all Song-mode lanes.</summary>
    public string GenreSongContext { get; set; } = "";
    /// <summary>Drum grammar supplied only to drum lanes in Song mode.</summary>
    public string GenreDrumContext { get; set; } = "";
}

public sealed class SongSectionMemory
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Plan { get; set; } = "";
    public string Summary { get; set; } = ""; // retained for compatibility.
    public string MemoryNotes { get; set; } = "";
    public int Bars { get; set; } = 8;
    /// <summary>Zero-based starting bar in the full song.</summary>
    public int StartBar { get; set; }
}

public sealed class SongMusicalMemory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "motif";
    public string Name { get; set; } = "";
    public string Notation { get; set; } = "";
    public string Description { get; set; } = "";
    public string SourceSectionId { get; set; } = "";
    public string SourceLane { get; set; } = "";
}

public sealed record SongGenerationContext(string SectionId, string Packet, string DirectorPacket);

/// <summary>
/// Deterministically provisions the small amount of song context needed for a section/lane call.
/// It never calls the LLM and never silently rewrites producer or musical memory.
/// </summary>
public static class SongGenerationProvisioner
{

    public static SongGenerationState Create(string brief, string design, int tempo, string meter, int targetBars, string composerDesign = "", GenreBriefResolver.Selection? genre = null)
    {
        if (string.IsNullOrWhiteSpace(brief)) throw new ArgumentException("A song brief is required.", nameof(brief));
        if (string.IsNullOrWhiteSpace(design)) throw new ArgumentException("Producer notes are required.", nameof(design));
        var sections = ParseDesignedSections(design, targetBars);
        if (sections.Count == 0) throw new InvalidDataException("Producer notes did not contain a usable section list.");
        int actualBars = sections.Sum(s => s.Bars);
        return new SongGenerationState
        {
            Brief = brief.Trim(),
            Title = SongCompositionDesigner.ExtractTitle(design, brief),
            Design = design.Trim(),
            ComposerDesign = (composerDesign ?? "").Trim(),
            Tempo = tempo,
            Meter = meter,
            TargetBars = actualBars,
            Sections = sections,
            GenreId = genre?.PrimaryId ?? "",
            GenreParentId = genre?.ParentId ?? "",
            GenreSecondaryId = genre?.SecondaryId ?? "",
            GenreSongModifierId = genre?.SongModifierId ?? "",
            GenreDrumId = genre?.DrumGenreId ?? "",
            GenreDrumFamilyIds = genre?.DrumFamilyIds ?? "",
            GenreDrumModifierId = genre?.DrumModifierId ?? "",
            GenreContext = "",
            GenreSongContext = genre?.SongPromptContext ?? "",
            GenreDrumContext = genre?.DrumPromptContext ?? ""
        };
    }

    public static string BuildPacket(SongGenerationState state, string sectionId, string laneName, bool drumLane)
    {
        ArgumentNullException.ThrowIfNull(state);
        var section = FindSection(state, sectionId);
        int currentIndex = Math.Max(0, state.Sections.FindIndex(s => string.Equals(s.Id, section.Id, StringComparison.OrdinalIgnoreCase)));
        var b = new StringBuilder();
        b.AppendLine("FULL SONG GENERATION CONTEXT")
            .AppendLine("The producer already planned the whole song. Do not call or imitate the producer. Generate only the current section/lane through the normal director and composer.")
            .AppendLine($"Original song request: {state.Brief}")
            .AppendLine($"Song tempo/meter: {state.Tempo} BPM, {state.Meter}")
            .AppendLine($"Current lane: {laneName}")
            .AppendLine()
            .AppendLine("PRODUCER NOTES")
            .AppendLine(state.Design);

        // New states store song and drum genre guidance separately so pitched lanes do not pay the
        // token cost of drum grammar. Older saved states may still contain one combined GenreContext.
        if (!string.IsNullOrWhiteSpace(state.GenreSongContext))
        {
            b.AppendLine()
                .AppendLine("SELECTED SONG-DESIGN GENRE GUIDANCE — SHARE WITH DIRECTOR AND COMPOSER")
                .AppendLine(state.GenreSongContext);
        }
        else if (!string.IsNullOrWhiteSpace(state.GenreContext))
        {
            b.AppendLine()
                .AppendLine("SELECTED GENRE GUIDANCE — LEGACY COMBINED CONTEXT")
                .AppendLine(state.GenreContext);
        }

        if (drumLane && !string.IsNullOrWhiteSpace(state.GenreDrumContext))
        {
            b.AppendLine()
                .AppendLine("SELECTED DRUM GENRE GUIDANCE — DRUM LANE ONLY")
                .AppendLine(state.GenreDrumContext);
        }

        if (!string.IsNullOrWhiteSpace(state.ComposerDesign))
        {
            b.AppendLine()
                .AppendLine("GLOBAL COMPOSER DESIGN PASS — SHARED MUSICAL DNA FOR EVERY TRACK")
                .AppendLine("This non-track-scoped material was composed before lane generation. Treat its exact Resonator melody seed, motifs, chord progression, answers, contrasts, continuations, key/AHD plan, and emotional-note palette as reusable source material for this lane. Adapt it to the lane role instead of ignoring it. Pitched lanes may quote or transform the pitches directly; bass/harmony should support its harmonic/emotional relationships; drums should reflect its rhythmic statement/response shapes where appropriate.")
                .AppendLine(state.ComposerDesign);
        }

        b.AppendLine()
            .AppendLine("ORDERED SECTION LIST");

        for (int i = 0; i < state.Sections.Count; i++)
        {
            var s = state.Sections[i];
            string marker = i == currentIndex ? " <== CURRENT" : "";
            b.AppendLine($"{i + 1}. [{s.Id}] {s.Title} — {s.Bars} bars, full-song bars {s.StartBar + 1}-{s.StartBar + s.Bars}{marker}");
        }

        b.AppendLine().AppendLine("CURRENT SECTION")
            .AppendLine(section.Plan);

        var priorNotes = state.Sections
            .Take(currentIndex + 1)
            .Where(s => !string.IsNullOrWhiteSpace(s.MemoryNotes))
            .ToList();
        if (priorNotes.Count != 0)
        {
            b.AppendLine().AppendLine("RECENT SONG MEMORY NOTES");
            foreach (var s in priorNotes)
                b.AppendLine($"[{s.Id} — {s.Title}]\n{s.MemoryNotes}");
        }

        var memories = state.MusicalMemories.ToList();
        if (memories.Count != 0)
        {
            b.AppendLine().AppendLine("EXACT MUSICAL MEMORY — USE WHEN RECALLING, ANSWERING, COUNTERING, OR TRANSFORMING MATERIAL");
            foreach (var m in memories)
            {
                b.Append($"- [{m.Kind}] {m.Name}");
                if (!string.IsNullOrWhiteSpace(m.Notation)) b.Append($" = `{m.Notation}`");
                if (!string.IsNullOrWhiteSpace(m.Description)) b.Append($" — {m.Description}");
                if (!string.IsNullOrWhiteSpace(m.SourceLane)) b.Append($" ({m.SourceLane})");
                b.AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(state.PendingComposerNotes))
        {
            b.AppendLine().AppendLine("OPEN COMPOSER COMMITMENTS — MUST BE FULFILLED OR CARRIED FORWARD")
                .AppendLine(state.PendingComposerNotes)
                .AppendLine("These are active promises made by earlier composer calls. Fulfill any commitment that belongs to the current section/lane. If a commitment is not yet due or cannot be fulfilled by this lane, copy it forward in the outgoing Next composer notes. Never silently drop an unfulfilled commitment.");
        }

        b.AppendLine().AppendLine("CONTINUITY RULE")
            .AppendLine("Use the producer's current-section goal and remembered exact musical material. Do not regenerate earlier sections. Preserve recognizable musical DNA when a section is meant to recall, answer, or counter prior material. An answer develops the remembered statement; a counter deliberately contrasts or reframes it. In either case, anchor response notes to the source notes at corresponding remembered positions and use the Interval Emotion Field Guide's Continuation property to choose the relationship. Rhythm, articulation, spacing, density, and timing may vary to make the answer/counter feel alive rather than copied.");
        return b.ToString().Trim();
    }

    public static string BuildDirectorPacket(SongGenerationState state, string sectionId, string laneName, bool drumLane)
    {
        ArgumentNullException.ThrowIfNull(state);
        var section = FindSection(state, sectionId);
        int currentIndex = Math.Max(0, state.Sections.FindIndex(s => string.Equals(s.Id, section.Id, StringComparison.OrdinalIgnoreCase)));
        var b = new StringBuilder();
        b.AppendLine("CURRENT SECTION SCOPE")
            .AppendLine($"Plan ONLY this {section.Bars}-bar section. Every phrase/bar reference must stay within local bars 1-{section.Bars}. Do not plan earlier or later song sections.")
            .AppendLine($"Original song request: {state.Brief}")
            .AppendLine($"Song tempo/meter: {state.Tempo} BPM, {state.Meter}")
            .AppendLine($"Current lane: {laneName}")
            .AppendLine()
            .AppendLine("CURRENT SECTION")
            .AppendLine(section.Plan);

        if (!string.IsNullOrWhiteSpace(state.GenreSongContext))
        {
            b.AppendLine()
                .AppendLine("SELECTED SONG-DESIGN GENRE GUIDANCE")
                .AppendLine(state.GenreSongContext);
        }
        else if (!string.IsNullOrWhiteSpace(state.GenreContext))
        {
            b.AppendLine()
                .AppendLine("SELECTED GENRE GUIDANCE — LEGACY COMBINED CONTEXT")
                .AppendLine(state.GenreContext);
        }

        if (drumLane && !string.IsNullOrWhiteSpace(state.GenreDrumContext))
        {
            b.AppendLine()
                .AppendLine("SELECTED DRUM GENRE GUIDANCE — CURRENT LANE ONLY")
                .AppendLine(state.GenreDrumContext);
        }

        if (!string.IsNullOrWhiteSpace(state.ComposerDesign))
        {
            b.AppendLine()
                .AppendLine("GLOBAL COMPOSER DESIGN PASS — SHARED MUSICAL DNA")
                .AppendLine(state.ComposerDesign);
        }

        var priorNotes = state.Sections
            .Take(currentIndex + 1)
            .Where(s => !string.IsNullOrWhiteSpace(s.MemoryNotes))
            .ToList();
        if (priorNotes.Count != 0)
        {
            b.AppendLine().AppendLine("RECENT SONG MEMORY NOTES");
            foreach (var s in priorNotes)
                b.AppendLine($"[{s.Id} — {s.Title}]\n{s.MemoryNotes}");
        }

        if (state.MusicalMemories.Count != 0)
        {
            b.AppendLine().AppendLine("EXACT MUSICAL MEMORY");
            foreach (var m in state.MusicalMemories)
            {
                b.Append($"- [{m.Kind}] {m.Name}");
                if (!string.IsNullOrWhiteSpace(m.Notation)) b.Append($" = `{m.Notation}`");
                if (!string.IsNullOrWhiteSpace(m.Description)) b.Append($" — {m.Description}");
                if (!string.IsNullOrWhiteSpace(m.SourceLane)) b.Append($" ({m.SourceLane})");
                b.AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(state.PendingComposerNotes))
        {
            b.AppendLine().AppendLine("OPEN COMPOSER COMMITMENTS")
                .AppendLine(state.PendingComposerNotes);
        }

        return b.ToString().Trim();
    }

    public static void ApplyChunkNotes(SongGenerationState state, string sectionId, string laneName, string memoryNotes)
    {
        if (string.IsNullOrWhiteSpace(memoryNotes)) return;
        var section = FindSection(state, sectionId);
        string block = memoryNotes.Trim();
        string stableMemory = BuildStableMemoryBlock(block);
        if (!string.IsNullOrWhiteSpace(stableMemory))
        {
            string tagged = $"[{laneName}]\n{stableMemory}";
            section.MemoryNotes = string.IsNullOrWhiteSpace(section.MemoryNotes)
                ? tagged
                : section.MemoryNotes + "\n\n" + tagged;
        }

        AddMemoryBlock(state, sectionId, laneName, block, "Melody notes", "melody");
        AddMemoryBlock(state, sectionId, laneName, block, "Motifs", "motif");
        AddMemoryBlock(state, sectionId, laneName, block, "Important chords", "chords");

        // The latest composer owns the active handoff list. It must carry every incoming item it did
        // not fulfill and may append new setup/payoff obligations. Historical section memory deliberately
        // excludes this field so stale obligations cannot be resurrected later.
        if (TryExtractField(block, "Next composer notes", out string nextComposerNotes))
        {
            state.PendingComposerNotes = IsNoOpenCommitment(nextComposerNotes)
                ? ""
                : nextComposerNotes.Trim();
        }

        int index = state.Sections.FindIndex(s => string.Equals(s.Id, section.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) state.CurrentSectionIndex = index;
    }

    private static SongSectionMemory FindSection(SongGenerationState state, string id)
    {
        var section = state.Sections.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
        if (section is not null) return section;
        section = new SongSectionMemory
        {
            Id = string.IsNullOrWhiteSpace(id) ? $"section-{state.Sections.Count + 1}" : id,
            Title = "Song section",
            StartBar = state.Sections.Sum(s => s.Bars)
        };
        state.Sections.Add(section);
        return section;
    }

    private static List<SongSectionMemory> ParseDesignedSections(string design, int targetBars)
    {
        // Producer output is intentionally plain text. Be strict about the semantic fields we need
        // (section id/title/bars) but tolerant about harmless header formatting differences. Some
        // local models omit the literal "SECTION N" prefix even when instructed and return:
        //     [INTRO] — Overture Build
        //     Bars: 4
        // Treat that as the same plan rather than throwing away a perfectly usable song design.
        int bodyStart = 0;
        int bodyEnd = design.Length;
        var sectionsLabel = Regex.Match(design, @"(?im)^\s*Sections\s*:\s*$");
        if (sectionsLabel.Success) bodyStart = sectionsLabel.Index + sectionsLabel.Length;
        var finalPayoff = Regex.Match(design[bodyStart..], @"(?im)^\s*Final\s+payoff\s*:");
        if (finalPayoff.Success) bodyEnd = bodyStart + finalPayoff.Index;

        string body = design[bodyStart..bodyEnd];
        var matches = Regex.Matches(body,
            @"(?im)^\s*(?:(?:SECTION\s+(?<n>\d+)(?:\s*\[(?<id>[^\]]+)\])?)|(?:\[(?<bareId>[^\]]+)\])|(?:(?<listN>\d+)[.)]\s*\[(?<listId>[^\]]+)\]))\s*(?:[—–:-]\s*)?(?<title>[^\r\n]*)$");
        if (matches.Count == 0) return [];

        var sections = new List<SongSectionMemory>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int cursorBar = 0;
        int fallback = Math.Max(1, targetBars / matches.Count);
        for (int i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            int end = i + 1 < matches.Count ? matches[i + 1].Index : body.Length;
            string plan = body[m.Index..end].Trim();
            string number = m.Groups["n"].Success ? m.Groups["n"].Value
                : m.Groups["listN"].Success ? m.Groups["listN"].Value
                : (i + 1).ToString();
            string rawId = m.Groups["id"].Success ? m.Groups["id"].Value
                : m.Groups["bareId"].Success ? m.Groups["bareId"].Value
                : m.Groups["listId"].Success ? m.Groups["listId"].Value
                : $"section-{number}";
            string id = NormalizeSectionId(rawId, $"section-{number}");
            string uniqueId = id;
            for (int suffix = 2; !usedIds.Add(uniqueId); suffix++) uniqueId = $"{id}-{suffix}";

            var barsMatch = Regex.Match(plan, @"(?im)^\s*Bars\s*:\s*(?<bars>\d+)\b");
            int bars = barsMatch.Success && int.TryParse(barsMatch.Groups["bars"].Value, out int parsed)
                ? Math.Clamp(parsed, 1, 64)
                : fallback;
            string title = m.Groups["title"].Value.Trim().TrimStart('—', '–', '-', ':').Trim();
            if (string.IsNullOrWhiteSpace(title)) title = $"Section {number}";

            sections.Add(new SongSectionMemory
            {
                Id = uniqueId,
                Title = title,
                Plan = plan,
                Bars = bars,
                StartBar = cursorBar
            });
            cursorBar += bars;
        }
        return sections;
    }

    private static string NormalizeSectionId(string value, string fallback)
    {
        string id = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9_-]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(id) ? fallback : id;
    }

    private static void AddMemoryBlock(SongGenerationState state, string sectionId, string laneName, string text, string heading, string kind)
    {
        string block = ExtractField(text, heading);
        if (string.IsNullOrWhiteSpace(block)) return;
        foreach (string raw in block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string line = raw.TrimStart('-', '*', ' ', '\t');
            if (line.Length < 2) continue;
            var ticks = Regex.Match(line, @"`(?<notation>[^`]+)`");
            string notation = ticks.Success ? ticks.Groups["notation"].Value.Trim() : "";
            string description = ticks.Success ? (line[..ticks.Index] + line[(ticks.Index + ticks.Length)..]).Trim(' ', '-', '—', ':') : line;
            string name = description;
            int colon = description.IndexOf(':');
            if (colon > 0) name = description[..colon].Trim();
            if (name.Length > 120) name = name[..120];
            if (state.MusicalMemories.Any(x => x.Kind == kind && x.Notation == notation && x.Description == description && x.SourceLane == laneName)) continue;
            state.MusicalMemories.Add(new SongMusicalMemory
            {
                Kind = kind,
                Name = string.IsNullOrWhiteSpace(name) ? heading : name,
                Notation = notation,
                Description = description,
                SourceSectionId = sectionId,
                SourceLane = laneName
            });
        }
    }

    private static string BuildStableMemoryBlock(string text)
    {
        var parts = new List<string>();
        foreach (string heading in new[] { "Melody notes", "Motifs", "Important chords" })
        {
            string value = ExtractField(text, heading);
            if (!string.IsNullOrWhiteSpace(value)) parts.Add($"{heading}: {value}");
        }
        return string.Join("\n", parts);
    }

    private static string ExtractField(string text, string heading)
        => TryExtractField(text, heading, out string value) ? value : "";

    private static bool TryExtractField(string text, string heading, out string value)
    {
        var m = Regex.Match(text, $@"(?ims)^\s*{Regex.Escape(heading)}\s*:\s*(?<value>.*?)(?=^\s*(?:Melody notes|Motifs|Important chords|Next composer notes)\s*:|\z)");
        value = m.Success ? m.Groups["value"].Value.Trim() : "";
        return m.Success;
    }

    private static bool IsNoOpenCommitment(string value)
        => string.IsNullOrWhiteSpace(value) || Regex.IsMatch(value.Trim(), @"^(?:\(?(?:none|nothing)\)?[.!]?|no\s+(?:open\s+)?(?:composer\s+)?(?:notes|commitments|obligations)[.!]?)$", RegexOptions.IgnoreCase);

}
