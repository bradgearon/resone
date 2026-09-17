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
    public string Design { get; set; } = "";
    public string Summary { get; set; } = ""; // retained for wire/backward compatibility; song mode no longer requires a prose roll-up.
    public int Tempo { get; set; } = 120;
    public string Meter { get; set; } = "4/4";
    public int TargetBars { get; set; } = 64;
    public int CurrentSectionIndex { get; set; }
    public List<SongSectionMemory> Sections { get; set; } = [];
    public List<SongMusicalMemory> MusicalMemories { get; set; } = [];
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

public sealed record SongGenerationContext(string SectionId, string Packet);

/// <summary>
/// Deterministically provisions the small amount of song context needed for a section/lane call.
/// It never calls the LLM and never silently rewrites producer or musical memory.
/// </summary>
public static class SongGenerationProvisioner
{
    private const int MaxDesignChars = 18000;
    private const int MaxSectionMemoryChars = 6000;
    private const int MaxMusicalMemories = 32;

    public static SongGenerationState Create(string brief, string design, int tempo, string meter, int targetBars)
    {
        if (string.IsNullOrWhiteSpace(brief)) throw new ArgumentException("A song brief is required.", nameof(brief));
        if (string.IsNullOrWhiteSpace(design)) throw new ArgumentException("Producer notes are required.", nameof(design));
        var sections = ParseDesignedSections(design, targetBars);
        if (sections.Count == 0) throw new InvalidDataException("Producer notes did not contain any SECTION entries.");
        return new SongGenerationState
        {
            Brief = brief.Trim(),
            Design = design.Trim(),
            Tempo = tempo,
            Meter = meter,
            TargetBars = targetBars,
            Sections = sections
        };
    }

    public static string BuildPacket(SongGenerationState state, string sectionId, string laneName)
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
            .AppendLine(Clip(state.Design, MaxDesignChars))
            .AppendLine()
            .AppendLine("ORDERED SECTION LIST");

        for (int i = 0; i < state.Sections.Count; i++)
        {
            var s = state.Sections[i];
            string marker = i == currentIndex ? " <== CURRENT" : "";
            b.AppendLine($"{i + 1}. [{s.Id}] {s.Title} — {s.Bars} bars, full-song bars {s.StartBar + 1}-{s.StartBar + s.Bars}{marker}");
        }

        b.AppendLine().AppendLine("CURRENT SECTION")
            .AppendLine(Clip(section.Plan, MaxSectionMemoryChars));

        var priorNotes = state.Sections
            .Take(currentIndex + 1)
            .Where(s => !string.IsNullOrWhiteSpace(s.MemoryNotes))
            .TakeLast(4)
            .ToList();
        if (priorNotes.Count != 0)
        {
            b.AppendLine().AppendLine("RECENT SONG MEMORY NOTES");
            foreach (var s in priorNotes)
                b.AppendLine($"[{s.Id} — {s.Title}]\n{Clip(s.MemoryNotes, 2400)}");
        }

        var memories = state.MusicalMemories.TakeLast(MaxMusicalMemories).ToList();
        if (memories.Count != 0)
        {
            b.AppendLine().AppendLine("EXACT MUSICAL MEMORY — USE WHEN RECALLING, ANSWERING, OR TRANSFORMING MATERIAL");
            foreach (var m in memories)
            {
                b.Append($"- [{m.Kind}] {m.Name}");
                if (!string.IsNullOrWhiteSpace(m.Notation)) b.Append($" = `{Clip(m.Notation, 700)}`");
                if (!string.IsNullOrWhiteSpace(m.Description)) b.Append($" — {Clip(m.Description, 700)}");
                if (!string.IsNullOrWhiteSpace(m.SourceLane)) b.Append($" ({m.SourceLane})");
                b.AppendLine();
            }
        }

        b.AppendLine().AppendLine("CONTINUITY RULE")
            .AppendLine("Use the producer's current-section goal and the remembered exact musical material. Do not regenerate earlier sections. Preserve recognizable musical DNA when a section is meant to recall or answer prior material.");
        return b.ToString().Trim();
    }

    public static void ApplyChunkNotes(SongGenerationState state, string sectionId, string laneName, string memoryNotes)
    {
        if (string.IsNullOrWhiteSpace(memoryNotes)) return;
        var section = FindSection(state, sectionId);
        string block = memoryNotes.Trim();
        string tagged = $"[{laneName}]\n{block}";
        section.MemoryNotes = string.IsNullOrWhiteSpace(section.MemoryNotes)
            ? tagged
            : Clip(section.MemoryNotes + "\n\n" + tagged, MaxSectionMemoryChars);

        AddMemoryBlock(state, sectionId, laneName, block, "Melody notes", "melody");
        AddMemoryBlock(state, sectionId, laneName, block, "Motifs", "motif");
        AddMemoryBlock(state, sectionId, laneName, block, "Important chords", "chords");

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
        var matches = Regex.Matches(design, @"(?im)^\s*SECTION\s+(?<n>\d+)(?:\s*\[(?<id>[^\]]+)\])?\s*[—:-]?\s*(?<title>.*)$");
        if (matches.Count == 0) return [];
        var sections = new List<SongSectionMemory>();
        int cursorBar = 0;
        int fallback = Math.Max(1, targetBars / matches.Count);
        for (int i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            int end = i + 1 < matches.Count ? matches[i + 1].Index : design.Length;
            string plan = design[m.Index..end].Trim();
            string id = m.Groups["id"].Success ? m.Groups["id"].Value.Trim() : $"section-{m.Groups["n"].Value}";
            var barsMatch = Regex.Match(plan, @"(?im)^\s*Bars\s*:\s*(?<bars>\d+)\b");
            int bars = barsMatch.Success && int.TryParse(barsMatch.Groups["bars"].Value, out int parsed) ? Math.Clamp(parsed, 1, 64) : fallback;
            sections.Add(new SongSectionMemory
            {
                Id = id,
                Title = string.IsNullOrWhiteSpace(m.Groups["title"].Value) ? $"Section {m.Groups["n"].Value}" : m.Groups["title"].Value.Trim(),
                Plan = plan,
                Bars = bars,
                StartBar = cursorBar
            });
            cursorBar += bars;
        }
        return sections;
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
        if (state.MusicalMemories.Count > 128)
            state.MusicalMemories.RemoveRange(0, state.MusicalMemories.Count - 128);
    }

    private static string ExtractField(string text, string heading)
    {
        var m = Regex.Match(text, $@"(?ims)^\s*{Regex.Escape(heading)}\s*:\s*(?<value>.*?)(?=^\s*(?:Melody notes|Motifs|Important chords)\s*:|\z)");
        return m.Success ? m.Groups["value"].Value.Trim() : "";
    }

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max] + "…";
}
