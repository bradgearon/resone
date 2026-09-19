using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Wds.Resone.Api.VocalSinging;

public sealed class VoicePitchProfile
{
    public const int CurrentVersion = 3;
    public int ProfileVersion { get; set; } = CurrentVersion;
    public float BasePitchHz { get; set; }
    public float LowPitchHz { get; set; }
    public float HighPitchHz { get; set; }
    public float VoicedFraction { get; set; }
    public int BaseMidiNote { get; set; } = -1;
    public int BaseOctave { get; set; } = -1;
    public int SingingMinMidiNote { get; set; } = -1;
    public int SingingMaxMidiNote { get; set; } = -1;

    [JsonIgnore]
    public bool IsValid => ProfileVersion >= CurrentVersion && BasePitchHz >= 40 && BaseMidiNote >= 0 && SingingMinMidiNote >= 0 && SingingMaxMidiNote > SingingMinMidiNote;
}

public sealed class SavedVoice
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Voice";
    public string Source { get; set; } = "designed";
    public string SampleFile { get; set; } = "";
    public string Transcript { get; set; } = "";
    public string DesignPrompt { get; set; } = "";
    public string SampleText { get; set; } = "";
    public string SpeakerFile { get; set; } = "";
    public string CodesFile { get; set; } = "";
    public int SpeakerDimension { get; set; }
    public int RefT { get; set; }
    public int NumCodebooks { get; set; }
    public VoicePitchProfile PitchProfile { get; set; } = new();
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class VoiceLibraryDocument
{
    public int Version { get; set; } = 1;
    public string LastSelectedVoiceId { get; set; } = "";
    public List<SavedVoice> Voices { get; set; } = [];
}

public sealed class VoicePreviewRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Source { get; set; } = "designed";
    public string SampleFile { get; set; } = "sample.wav";
    public string Transcript { get; set; } = "";
    public string DesignPrompt { get; set; } = "";
    public string SampleText { get; set; } = "";
    public string SingingFile { get; set; } = "";
    public string MelodyId { get; set; } = "fun";
    public VoicePitchProfile PitchProfile { get; set; } = new();
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class VoiceLibraryStore
{
    private static readonly object Sync = new();
    private readonly string root;
    private readonly string audioRoot;
    private readonly string refsRoot;
    private readonly string previewsRoot;
    private readonly string libraryPath;

    public VoiceLibraryStore()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        root = Path.Combine(local, "Wds", "Resone", "user", "voices");
        audioRoot = Path.Combine(root, "audio"); refsRoot = Path.Combine(root, "refs"); previewsRoot = Path.Combine(root, "previews");
        libraryPath = Path.Combine(root, "library.json");
        Directory.CreateDirectory(audioRoot); Directory.CreateDirectory(refsRoot); Directory.CreateDirectory(previewsRoot);
        CleanupPreviews();
    }

    public string NewPreviewDirectory(string id)
    {
        ValidateId(id); string path = Path.Combine(previewsRoot, id); Directory.CreateDirectory(path); return path;
    }

    public void SavePreview(VoicePreviewRecord preview)
    {
        ValidateId(preview.Id);
        string dir = Path.Combine(previewsRoot, preview.Id); Directory.CreateDirectory(dir);
        AtomicJson(Path.Combine(dir, "preview.json"), preview, ResoneJson.Default.VoicePreviewRecord);
    }

    public VoicePreviewRecord LoadPreview(string id)
    {
        ValidateId(id);
        string path = Path.Combine(previewsRoot, id, "preview.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Voice preview expired or was not found.", path);
        return JsonSerializer.Deserialize(File.ReadAllText(path), ResoneJson.Default.VoicePreviewRecord)
            ?? throw new InvalidDataException("Voice preview metadata is invalid.");
    }

    public string PreviewFile(string id, string name)
    {
        ValidateId(id); return Path.Combine(previewsRoot, id, name);
    }

    public void DiscardPreview(string id)
    {
        ValidateId(id); string dir = Path.Combine(previewsRoot, id);
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }

    public VoiceLibraryDocument Load()
    {
        lock (Sync)
        {
            if (!File.Exists(libraryPath)) return new VoiceLibraryDocument();
            try { return JsonSerializer.Deserialize(File.ReadAllText(libraryPath), ResoneJson.Default.VoiceLibraryDocument) ?? new(); }
            catch { return new VoiceLibraryDocument(); }
        }
    }

    public SavedVoice Get(string id)
    {
        ValidateId(id);
        var doc = Load();
        return doc.Voices.FirstOrDefault(v => string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException("The selected voice is no longer in the Resone voice library.");
    }

    public string SamplePath(SavedVoice voice) => ResolveStored(voice.SampleFile);

    public SavedVoice EnsurePitchProfile(SavedVoice voice)
    {
        if (voice.PitchProfile?.IsValid == true) return voice;
        return ReanalyzePitchProfile(voice);
    }

    public SavedVoice ReanalyzePitchProfile(SavedVoice voice)
    {
        string sample = SamplePath(voice);
        var analyzed = VocalSingingEngine.AnalyzeVoiceProfile(sample);
        lock (Sync)
        {
            var doc = Load();
            var stored = doc.Voices.FirstOrDefault(v => string.Equals(v.Id, voice.Id, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException("The selected voice is no longer in the Resone voice library.");
            stored.PitchProfile = analyzed;
            stored.UpdatedUtc = DateTimeOffset.UtcNow;
            SaveDocument(doc);
            return stored;
        }
    }

    public SavedVoice SavePreviewAsVoice(string previewId, string name)
    {
        name = NormalizeName(name);
        var preview = LoadPreview(previewId);
        string previewSample = PreviewFile(previewId, preview.SampleFile);
        if (!File.Exists(previewSample)) throw new FileNotFoundException("Voice preview audio was not found.", previewSample);
        lock (Sync)
        {
            var doc = Load();
            if (doc.Voices.Any(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A saved voice named '{name}' already exists. Choose another name.");
            string id = Guid.NewGuid().ToString("N");
            string sampleRel = Path.Combine("audio", id + ".wav");
            File.Copy(previewSample, Path.Combine(root, sampleRel), true);
            var voice = new SavedVoice
            {
                Id = id, Name = name, Source = preview.Source, SampleFile = sampleRel, Transcript = preview.Transcript,
                DesignPrompt = preview.DesignPrompt, SampleText = preview.SampleText, PitchProfile = preview.PitchProfile,
                CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow
            };
            doc.Voices.Add(voice); doc.LastSelectedVoiceId = id; SaveDocument(doc);
            DiscardPreview(previewId);
            return voice;
        }
    }

    public void Select(string id)
    {
        ValidateId(id);
        lock (Sync)
        {
            var doc = Load();
            if (!doc.Voices.Any(v => string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase))) throw new KeyNotFoundException("Voice not found.");
            doc.LastSelectedVoiceId = id; SaveDocument(doc);
        }
    }

    public JsonObject ToPayload()
    {
        var doc = Load();
        var voices = new JsonArray(doc.Voices.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).Select(v => (JsonNode?)new JsonObject
        {
            ["id"] = v.Id, ["name"] = v.Name, ["source"] = v.Source, ["transcript"] = v.Transcript,
            ["designPrompt"] = v.DesignPrompt, ["createdUtc"] = v.CreatedUtc.ToString("O"),
            ["profileVersion"] = v.PitchProfile?.ProfileVersion ?? 0, ["basePitchHz"] = v.PitchProfile?.BasePitchHz ?? 0, ["baseMidiNote"] = v.PitchProfile?.BaseMidiNote ?? -1,
            ["baseOctave"] = v.PitchProfile?.BaseOctave ?? -1, ["singingMinMidiNote"] = v.PitchProfile?.SingingMinMidiNote ?? -1,
            ["singingMaxMidiNote"] = v.PitchProfile?.SingingMaxMidiNote ?? -1
        }).ToArray());
        var melodies = new JsonArray(VoicePreviewMelodies.All.Select(x => (JsonNode?)new JsonObject
        { ["id"] = x.Id, ["name"] = x.Name, ["feeling"] = x.Feeling }).ToArray());
        return new JsonObject { ["voices"] = voices, ["lastSelectedVoiceId"] = doc.LastSelectedVoiceId, ["previewMelodies"] = melodies };
    }

    private void SaveDocument(VoiceLibraryDocument doc) => AtomicJson(libraryPath, doc, ResoneJson.Default.VoiceLibraryDocument);

    private string ResolveStored(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) throw new InvalidDataException("Saved voice file path is empty.");
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Saved voice path escaped the voice library.");
        return full;
    }

    private static void AtomicJson<T>(string path, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, info));
        File.Move(temp, path, true);
    }

    private static string NormalizeName(string value)
    {
        string name = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (name.Length is < 1 or > 80) throw new ArgumentException("Voice name must be 1–80 characters.");
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("Voice name contains invalid characters.");
        return name;
    }

    private static void ValidateId(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid voice id.");
    }

    private void CleanupPreviews()
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(previewsRoot))
                if (Directory.GetCreationTimeUtc(dir) < DateTime.UtcNow.AddDays(-1)) Directory.Delete(dir, true);
        }
        catch { }
    }
}
