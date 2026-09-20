using System.Text;
using System.Text.RegularExpressions;

namespace Wds.Resone.Api;

/// <summary>
/// Deterministic, fail-soft retrieval of small genre briefs from Resone's genre guides.
/// This resolver never calls an LLM. Song mode supplies the producer identity; ordinary drum-lane
/// mode may supply a tiny separately identified genre label. This layer only retrieves guide material.
/// </summary>
public static class GenreBriefResolver
{
    private sealed record GuideEntry(string Id, string Body, IReadOnlyList<string> Aliases, string ParentId);

    private static readonly IReadOnlyDictionary<string, string> ParentGenre = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["alternative_indie_rock"] = "rock",
        ["punk_hardcore_punk"] = "rock",
        ["post_rock"] = "rock",
        ["thrash_metal"] = "metal",
        ["death_metal"] = "metal",
        ["black_metal"] = "metal",
        ["doom_sludge"] = "metal",
        ["power_symphonic_metal"] = "metal",
        ["metalcore"] = "metal",
        ["djent_polyrhythmic_metal"] = "metal",
        ["progressive_metal"] = "metal",
        ["polyrhythmic_progressive_metal"] = "progressive_metal",
        ["progressive_rock"] = "rock",
        ["psychedelic_latin_progressive_rock"] = "progressive_rock",
        ["house"] = "edm",
        ["techno"] = "edm",
        ["electro_breakbeat"] = "edm",
        ["dubstep_bass_music"] = "edm",
        ["drum_and_bass"] = "edm",
        ["trance"] = "edm",
        ["uplifting_trance"] = "trance",
        ["psytrance"] = "trance",
        ["hardcore_gabber"] = "edm",
        ["hardstyle"] = "edm",
        ["boom_bap"] = "hip_hop_rap",
        ["trap"] = "hip_hop_rap",
        ["drill"] = "hip_hop_rap",
        ["lofi_hip_hop"] = "hip_hop_rap",
        ["romantic_classical"] = "classical",
        ["neoclassical_modern_classical"] = "classical"
    };

    public sealed record Selection(
        string PrimaryId,
        string ParentId,
        string SecondaryId,
        string SongModifierId,
        string DrumGenreId,
        string DrumFamilyIds,
        string DrumModifierId,
        int Confidence,
        string SongBrief,
        string DrumBrief)
    {
        public static readonly Selection Empty = new("", "", "", "", "", "", "", 0, "", "");
        public bool HasMatch => !string.IsNullOrWhiteSpace(PrimaryId) || !string.IsNullOrWhiteSpace(DrumGenreId);

        public string SongPromptContext
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SongBrief)) return "";
                var b = new StringBuilder()
                    .AppendLine("SELECTED SONG-DESIGN GENRE BRIEF — RETRIEVED, NOT AUTHORITATIVE")
                    .AppendLine("The user's request and existing song remain authoritative. Use this as genre grammar without erasing established motifs, emotion, meter, AHD relationships, or deliberate exceptions.")
                    .AppendLine($"Primary genre: {(string.IsNullOrWhiteSpace(PrimaryId) ? "(none)" : PrimaryId)}");
                if (!string.IsNullOrWhiteSpace(ParentId)) b.AppendLine($"Parent grammar: {ParentId}");
                if (!string.IsNullOrWhiteSpace(SecondaryId)) b.AppendLine($"Secondary / hybrid role: {SecondaryId}");
                if (!string.IsNullOrWhiteSpace(SongModifierId)) b.AppendLine($"Modifier: {SongModifierId}");
                return b.AppendLine(SongBrief.Trim()).ToString().Trim();
            }
        }

        public string DrumPromptContext
        {
            get
            {
                if (string.IsNullOrWhiteSpace(DrumBrief)) return "";
                var b = new StringBuilder()
                    .AppendLine("SELECTED DRUM / PERCUSSION GENRE BRIEF — RETRIEVED, NOT AUTHORITATIVE")
                    .AppendLine("The user's rhythm and existing song are authoritative. Apply only the matched pulse/pocket/fill/energy grammar; do not import unrelated genre traits.")
                    .AppendLine($"Specific drum grammar: {(string.IsNullOrWhiteSpace(DrumGenreId) ? "(core only)" : DrumGenreId)}");
                if (!string.IsNullOrWhiteSpace(DrumFamilyIds)) b.AppendLine($"Family inheritance: {DrumFamilyIds}");
                if (!string.IsNullOrWhiteSpace(DrumModifierId)) b.AppendLine($"Modifier: {DrumModifierId}");
                return b.AppendLine(DrumBrief.Trim()).ToString().Trim();
            }
        }

        public string PromptContext
        {
            get
            {
                var parts = new[] { SongPromptContext, DrumPromptContext }.Where(x => !string.IsNullOrWhiteSpace(x));
                return string.Join("\n\n", parts);
            }
        }
    }

    public static Selection Resolve(string assetsRoot, string identityOrRequest, string? originalRequest = null)
    {
        try
        {
            string identity = identityOrRequest ?? "";
            string original = originalRequest ?? "";
            string identityNorm = Normalize(identity);
            string queryNorm = Normalize(string.Join(" ", new[] { identity, original }.Where(x => !string.IsNullOrWhiteSpace(x))));
            if (string.IsNullOrWhiteSpace(queryNorm)) return Selection.Empty;

            string songText = InstructionContent.Read(assetsRoot, "resone_song_design_genre_guide.md");
            string drumText = InstructionContent.Read(assetsRoot, "resone_drums_genre_guide.md");
            var songGenres = ParseTopLevelGenreEntries(songText);
            var drumGenres = ParseTopLevelGenreEntries(drumText);
            if (songGenres.Count == 0 && drumGenres.Count == 0) return Selection.Empty;

            var songRanked = Rank(identityNorm, queryNorm, songGenres.Values);
            string primary = songRanked.Count != 0 && songRanked[0].Score >= 430 ? songRanked[0].Entry.Id : "";
            int songConfidence = string.IsNullOrWhiteSpace(primary) ? 0 : songRanked[0].Score;
            string secondary = "";

            // Explicit hybrid handling from the song-design guide. Jobs stay distinct rather than
            // averaging two unrelated genre grammars.
            if (ContainsPhrase(queryNorm, "trap metal") && songGenres.ContainsKey("trap") && songGenres.ContainsKey("metal"))
            {
                primary = "trap";
                secondary = "metal";
                songConfidence = Math.Max(songConfidence, 1200);
            }
            else if (!string.IsNullOrWhiteSpace(primary)
                     && !string.Equals(primary, "cinematic_orchestral", StringComparison.OrdinalIgnoreCase)
                     && MentionsCinematic(queryNorm)
                     && songGenres.ContainsKey("cinematic_orchestral"))
            {
                secondary = "cinematic_orchestral";
            }

            string parent = ParentGenre.TryGetValue(primary, out string? p) ? p : "";
            if (string.Equals(parent, secondary, StringComparison.OrdinalIgnoreCase)) secondary = "";

            var songModifiers = ParseModifierEntries(songText);
            var drumModifiers = ParseModifierEntries(drumText);
            string songModifier = SelectSongModifier(queryNorm, songModifiers);
            string drumModifier = SelectDrumModifier(queryNorm, drumModifiers);

            // Drum retrieval is intentionally independent and can be more specific than the song
            // design ID (deep_house, classic_trance, jungle, grunge, nu_metal, etc.).
            var drumCandidates = drumGenres.Values.Where(e => !string.Equals(e.Id, "drum_core", StringComparison.OrdinalIgnoreCase));
            var drumRanked = Rank(identityNorm, queryNorm, drumCandidates);
            string drumGenre = drumRanked.Count != 0 && drumRanked[0].Score >= 430 ? drumRanked[0].Entry.Id : "";
            int drumConfidence = string.IsNullOrWhiteSpace(drumGenre) ? 0 : drumRanked[0].Score;
            if (string.IsNullOrWhiteSpace(drumGenre) && !string.IsNullOrWhiteSpace(primary) && drumGenres.ContainsKey(primary))
                drumGenre = primary;

            var songIds = new List<string>();
            AppendFamilyChain(songIds, primary, songGenres, ParentGenre);
            AppendFamilyChain(songIds, secondary, songGenres, ParentGenre);
            if (!string.IsNullOrWhiteSpace(songModifier)) songIds.Add(songModifier);
            string songBrief = CombineEntries(songGenres, songModifiers, songIds.ToArray());

            var drumIds = new List<string> { "drum_core" };
            AppendFamilyChain(drumIds, drumGenre, drumGenres, ParentGenre);
            // A documented song hybrid also contributes its compatible drum family as a separate job.
            if (!string.IsNullOrWhiteSpace(secondary)) AppendFamilyChain(drumIds, secondary, drumGenres, ParentGenre);
            if (!string.IsNullOrWhiteSpace(drumModifier)) drumIds.Add(drumModifier);
            string drumBrief = CombineEntries(drumGenres, drumModifiers, drumIds.ToArray());

            string drumFamilies = string.Join(" -> ", GetFamilyChain(drumGenre, drumGenres, ParentGenre));
            int confidence = Math.Max(songConfidence, drumConfidence);
            return new Selection(primary, parent, secondary, songModifier, drumGenre, drumFamilies, drumModifier, confidence, songBrief, drumBrief);
        }
        catch
        {
            // Genre guidance is enrichment. Missing/edited/malformed guide files must never make
            // composition fail; producer/director/composer continue normally without a genre match.
            return Selection.Empty;
        }
    }

    private static List<(GuideEntry Entry, int Score)> Rank(string identityNorm, string queryNorm, IEnumerable<GuideEntry> entries) =>
        entries.Select(e => (Entry: e, Score: Score(identityNorm, queryNorm, e)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Entry.Id.Length)
            .ToList();

    private static IReadOnlyList<string> GetFamilyChain(
        string id,
        IReadOnlyDictionary<string, GuideEntry> genres,
        IReadOnlyDictionary<string, string> fallbackParents)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string current = id ?? "";
        while (!string.IsNullOrWhiteSpace(current) && seen.Add(current))
        {
            result.Add(current);
            string next = "";
            if (genres.TryGetValue(current, out var entry) && !string.IsNullOrWhiteSpace(entry.ParentId))
                next = entry.ParentId;
            else if (fallbackParents.TryGetValue(current, out string? fallback))
                next = fallback;
            current = next ?? "";
        }
        result.Reverse(); // broad fundamentals first, specific override last.
        return result;
    }

    private static void AppendFamilyChain(
        ICollection<string> destination,
        string id,
        IReadOnlyDictionary<string, GuideEntry> genres,
        IReadOnlyDictionary<string, string> fallbackParents)
    {
        foreach (string item in GetFamilyChain(id, genres, fallbackParents))
            if (!destination.Contains(item, StringComparer.OrdinalIgnoreCase)) destination.Add(item);
    }

    private static Dictionary<string, GuideEntry> ParseTopLevelGenreEntries(string text)
    {
        var result = new Dictionary<string, GuideEntry>(StringComparer.OrdinalIgnoreCase);
        string source = text ?? "";
        var allHeadings = Regex.Matches(source, @"(?m)^# (?<heading>[^\r\n]+)\s*$");
        for (int i = 0; i < allHeadings.Count; i++)
        {
            string id = allHeadings[i].Groups["heading"].Value.Trim();
            if (!Regex.IsMatch(id, @"^[a-z0-9_]+$")) continue;
            int start = allHeadings[i].Index;
            int end = i + 1 < allHeadings.Count ? allHeadings[i + 1].Index : source.Length;
            string body = source[start..end].Trim();
            var aliases = ExtractAliases(body, id);
            result[id] = new GuideEntry(id, body, aliases, ExtractParent(body));
        }
        return result;
    }

    private static Dictionary<string, GuideEntry> ParseModifierEntries(string text)
    {
        var result = new Dictionary<string, GuideEntry>(StringComparer.OrdinalIgnoreCase);
        var matches = Regex.Matches(text ?? "", @"(?m)^## (?<id>[a-z0-9_]+)\s*$");
        for (int i = 0; i < matches.Count; i++)
        {
            string id = matches[i].Groups["id"].Value;
            int start = matches[i].Index;
            int next = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            int top = text.IndexOf("\n# ", start + 1, StringComparison.Ordinal);
            int end = top >= 0 && top < next ? top + 1 : next;
            string body = text[start..end].Trim();
            result[id] = new GuideEntry(id, body, [Normalize(id.Replace('_', ' '))], "");
        }
        return result;
    }

    private static string ExtractParent(string body)
    {
        var m = Regex.Match(body, @"(?im)^\*\*Drum parent:\*\*\s*`?(?<parent>[a-z0-9_]+)`?");
        return m.Success ? m.Groups["parent"].Value.Trim() : "";
    }

    private static IReadOnlyList<string> ExtractAliases(string body, string id)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Normalize(id.Replace('_', ' ')) };
        var m = Regex.Match(body, @"(?im)^\*\*Aliases:\*\*\s*(?<aliases>[^\r\n]+)");
        if (m.Success)
        {
            foreach (string raw in m.Groups["aliases"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string normalized = Normalize(raw);
                if (!string.IsNullOrWhiteSpace(normalized)) aliases.Add(normalized);
            }
        }
        return aliases.ToList();
    }

    private static int Score(string identityNorm, string queryNorm, GuideEntry entry)
    {
        int best = 0;
        foreach (string alias in entry.Aliases)
        {
            if (string.IsNullOrWhiteSpace(alias)) continue;
            int words = alias.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (identityNorm == alias) best = Math.Max(best, 1400 + words * 40 + alias.Length);
            if (ContainsPhrase(identityNorm, alias)) best = Math.Max(best, 1100 + words * 40 + alias.Length);
            if (ContainsPhrase(queryNorm, alias)) best = Math.Max(best, 900 + words * 35 + alias.Length);

            string[] aliasTokens = alias.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string[] queryTokens = queryNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (aliasTokens.Length == 0 || queryTokens.Length == 0) continue;
            double sum = 0;
            foreach (string token in aliasTokens)
                sum += queryTokens.Max(q => TokenSimilarity(token, q));
            double average = sum / aliasTokens.Length;
            if (average >= 0.84)
                best = Math.Max(best, (int)Math.Round(average * 500) + aliasTokens.Length * 20);
        }
        return best;
    }

    private static string CombineEntries(
        IReadOnlyDictionary<string, GuideEntry> genres,
        IReadOnlyDictionary<string, GuideEntry> modifiers,
        params string[] ids)
    {
        var b = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in ids)
        {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) continue;
            if (genres.TryGetValue(id, out var genre)) b.AppendLine(genre.Body).AppendLine();
            else if (modifiers.TryGetValue(id, out var modifier)) b.AppendLine(modifier.Body).AppendLine();
        }
        return b.ToString().Trim();
    }

    private static string SelectSongModifier(string query, IReadOnlyDictionary<string, GuideEntry> modifiers)
    {
        string id = "";
        if (HasAny(query, "radiant", "transcendent", "transcendental")) id = "radiant_transcendent";
        else if (HasAny(query, "dark", "ominous", "menacing")) id = "dark_ominous";
        else if (HasAny(query, "heroic", "heroism")) id = "heroic";
        else if (HasAny(query, "psychedelic", "psychedelia")) id = "psychedelic";
        else if (HasAny(query, "minimal", "minimalist")) id = "minimal";
        else if (HasAny(query, "funky", "funk")) id = "funky_syncopated";
        return modifiers.ContainsKey(id) ? id : "";
    }

    private static string SelectDrumModifier(string query, IReadOnlyDictionary<string, GuideEntry> modifiers)
    {
        string id = "";
        if (ContainsPhrase(query, "more energy") || ContainsPhrase(query, "higher energy")) id = "more_energy";
        else if (ContainsPhrase(query, "less energy") || ContainsPhrase(query, "lower energy")) id = "less_energy";
        else if (ContainsPhrase(query, "more human") || ContainsPhrase(query, "humanized")) id = "more_human";
        else if (ContainsPhrase(query, "more mechanical") || ContainsPhrase(query, "mechanical drums")) id = "more_mechanical";
        else if (HasAny(query, "polyrhythmic", "polyrhythm")) id = "more_polyrhythmic";
        else if (ContainsPhrase(query, "more cinematic") || ContainsPhrase(query, "cinematic percussion")) id = "more_cinematic";
        return modifiers.ContainsKey(id) ? id : "";
    }

    private static bool MentionsCinematic(string query) =>
        HasAny(query, "cinematic", "orchestral", "soundtrack") || ContainsPhrase(query, "film score") || ContainsPhrase(query, "game score");

    private static bool HasAny(string query, params string[] words) => words.Any(w => ContainsPhrase(query, Normalize(w)));

    private static bool ContainsPhrase(string query, string phrase)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(phrase)) return false;
        return (" " + query + " ").Contains(" " + phrase + " ", StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        string lowered = value.ToLowerInvariant().Replace('&', ' ');
        lowered = Regex.Replace(lowered, @"[^a-z0-9]+", " ");
        return Regex.Replace(lowered, @"\s+", " ").Trim();
    }

    private static double TokenSimilarity(string a, string b)
    {
        if (a == b) return 1;
        if (a.Length <= 3 || b.Length <= 3) return 0;
        int distance = Levenshtein(a, b);
        return Math.Max(0, 1.0 - distance / (double)Math.Max(a.Length, b.Length));
    }

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) previous[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}
