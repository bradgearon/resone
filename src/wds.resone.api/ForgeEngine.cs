using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using Wds.Resone.Api.VocalSinging;
using Wds.Resone.Api.Ai;
using Wds.Resone.Api.Music;
using Wds.Resone.Resonator;

namespace Wds.Resone.Api;
/// <summary>Composition root: music AI -> validated notation -> Resonator -> explicit note timeline.</summary>
public sealed class ForgeEngine(ResoneSettings settings, string assetsRoot, HttpClient http, QwenTtsServiceManager? ttsManager = null)
{
    public async Task<JsonObject> ComposeAsync(JsonElement payload, CancellationToken token, Func<string, Task>? progress = null)
    {
        var project = payload.GetProperty("project").Deserialize(ResoneJson.Default.SongProject) ?? throw new ArgumentException("Missing project.");
        ValidateProject(project);
        string laneId = payload.GetProperty("laneId").GetString() ?? "";
        if (!project.Lanes.Any(l => l.Id == laneId)) throw new ArgumentException("Select a lane to generate.");
        string description = payload.GetProperty("description").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(description) || description.Length > 12000)
            throw new ArgumentException("Describe the music in 1–12000 characters.");
        bool useAhd = !payload.TryGetProperty("useAhd", out var ahd) || ahd.GetBoolean();
        string composerOverview = payload.TryGetProperty("composerOverview", out var overviewValue) ? overviewValue.GetString() ?? "" : "";
        return await new ArrangementComposer(http, settings, assetsRoot).ComposeAsync(project, laneId, description, useAhd, composerOverview, token, progress);
    }


    public async Task<JsonObject> DesignSongAsync(JsonElement payload, CancellationToken token, Func<string, Task>? progress = null)
    {
        string description = payload.GetProperty("description").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(description) || description.Length > 12000)
            throw new ArgumentException("Describe the song in 1–12000 characters.");
        int tempo = payload.TryGetProperty("tempo", out var tempoValue) ? tempoValue.GetInt32() : 120;
        string meter = payload.TryGetProperty("meter", out var meterValue) ? meterValue.GetString() ?? "4/4" : "4/4";
        int targetBars = payload.TryGetProperty("targetBars", out var barsValue) ? barsValue.GetInt32() : 96;
        bool useAhd = !payload.TryGetProperty("useAhd", out var ahdValue) || ahdValue.GetBoolean();
        bool composerDesignPass = !payload.TryGetProperty("composerDesignPass", out var composerDesignValue) || composerDesignValue.GetBoolean();
        string existingComposerOverview = payload.TryGetProperty("composerOverview", out var existingOverviewValue) ? existingOverviewValue.GetString() ?? "" : "";
        if (!ResonatorNotationValidator.IsMeter(meter)) throw new ArgumentException("Invalid song meter.");
        if (progress is not null) await progress("Song · Producing outline…").ConfigureAwait(false);
        ILocalChatModelClient client = settings.LocalInferenceEnabled ? new NativeChatClient(settings) : new LocalAiClient(http, settings);
        // Before the producer has chosen an Overall identity, retrieve a best-effort brief from the
        // user's words. After the producer runs we re-resolve from its identity and use that final
        // canonical selection for every later composer/director call.
        var producerGenre = GenreBriefResolver.Resolve(assetsRoot, description);
        string design = await SongCompositionDesigner.CreateAsync(
            client, description, tempo, meter, targetBars, useAhd, existingComposerOverview, producerGenre.PromptContext, token).ConfigureAwait(false);
        string identity = SongCompositionDesigner.ExtractOverallIdentity(design);
        var selectedGenre = GenreBriefResolver.Resolve(assetsRoot, identity, description);
        if (!selectedGenre.HasMatch) selectedGenre = producerGenre;
        string composerDesign = existingComposerOverview;
        if (composerDesignPass)
        {
            if (progress is not null) await progress("Song · Composer design pass · motifs, harmony, and responses…").ConfigureAwait(false);
            composerDesign = await SongComposerDesignPass.CreateAsync(
                client, assetsRoot, description, design, tempo, meter, targetBars, useAhd, existingComposerOverview, selectedGenre.PromptContext, token).ConfigureAwait(false);
        }
        var state = SongGenerationProvisioner.Create(description, design, tempo, meter, targetBars, composerDesign, selectedGenre);
        return new JsonObject
        {
            ["design"] = design,
            ["composerDesign"] = composerDesign,
            ["title"] = state.Title,
            ["genreId"] = state.GenreId,
            ["state"] = JsonSerializer.SerializeToNode(state, ResoneJson.Default.SongGenerationState)
        };
    }

    public async Task<JsonObject> ComposeSongChunkAsync(JsonElement payload, CancellationToken token, Func<string, Task>? progress = null)
    {
        var project = payload.GetProperty("project").Deserialize(ResoneJson.Default.SongProject) ?? throw new ArgumentException("Missing project.");
        ValidateProject(project);
        var state = payload.GetProperty("songState").Deserialize(ResoneJson.Default.SongGenerationState) ?? throw new ArgumentException("Missing song generation state.");
        string sectionId = payload.GetProperty("sectionId").GetString() ?? "";
        string laneId = payload.GetProperty("laneId").GetString() ?? "";
        var lane = project.Lanes.SingleOrDefault(l => l.Id == laneId) ?? throw new ArgumentException("Select a lane to generate.");
        string description = payload.GetProperty("description").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(description) || description.Length > 12000)
            throw new ArgumentException("Describe this song section/lane in 1–12000 characters.");
        bool useAhd = !payload.TryGetProperty("useAhd", out var ahd) || ahd.GetBoolean();
        string packet = SongGenerationProvisioner.BuildPacket(state, sectionId, lane.Name, lane.Drums);
        int sectionIndex = Math.Max(0, state.Sections.FindIndex(s => string.Equals(s.Id, sectionId, StringComparison.OrdinalIgnoreCase)));
        Func<string, Task>? songProgress = progress is null ? null : message =>
        {
            string stage = message.Contains("Directing", StringComparison.OrdinalIgnoreCase) ? "Directing…"
                : message.Contains("Arranging", StringComparison.OrdinalIgnoreCase) ? "Arranging…"
                : message.Contains("Rendering", StringComparison.OrdinalIgnoreCase) ? "Rendering MIDI…"
                : message;
            return progress($"Song · Section {sectionIndex + 1}/{Math.Max(1, state.Sections.Count)} · {lane.Name} · {stage}");
        };
        var result = await new ArrangementComposer(http, settings, assetsRoot).ComposeSongChunkAsync(
            project, laneId, description, useAhd, new SongGenerationContext(sectionId, packet), token, songProgress).ConfigureAwait(false);
        string memory = result["songMemoryNotes"]?.GetValue<string>() ?? "";
        SongGenerationProvisioner.ApplyChunkNotes(state, sectionId, lane.Name, memory);
        result["songState"] = JsonSerializer.SerializeToNode(state, ResoneJson.Default.SongGenerationState);
        return result;
    }

    public async Task<JsonObject> RenderVocalsAsync(JsonElement payload, CancellationToken token, Func<string, Task>? progress = null)
    {
        var project = payload.GetProperty("project").Deserialize(ResoneJson.Default.SongProject)
            ?? throw new ArgumentException("Missing project.");
        ValidateProject(project);
        string laneId = payload.GetProperty("laneId").GetString() ?? "";
        var lane = project.Lanes.SingleOrDefault(l => l.Id == laneId)
            ?? throw new ArgumentException("Select a vocals lane to render.");
        if (!lane.Vocals || lane.Drums)
            throw new ArgumentException("The selected lane is not a vocals lane.");
        if (lane.Notes.Count == 0)
            throw new ArgumentException("Create or import a vocal melody before rendering singing.");

        string text = payload.TryGetProperty("text", out var textValue) ? textValue.GetString() ?? "" : lane.Lyrics;
        string voiceId = payload.TryGetProperty("voiceId", out var voiceValue) ? voiceValue.GetString() ?? "default" : lane.VoiceId;
        text = text.Trim();
        voiceId = string.IsNullOrWhiteSpace(voiceId) ? "default" : voiceId.Trim();
        if (text.Length is < 1 or > 12000)
            throw new ArgumentException("Enter 1–12000 characters of lyrics/text for the vocal lane.");

        static string UserVocalDirectory()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string path = Path.Combine(local, "Wds", "Resone", "user", "vocals");
            Directory.CreateDirectory(path);
            return path;
        }
        static string LaneFileKey(string id)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))).Substring(0, 20).ToLowerInvariant();

        string temp = Path.Combine(Path.GetTempPath(), "Resone", "vocals", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        string speech = Path.Combine(temp, "speech.wav");
        string midiPath = Path.Combine(temp, "vocal.mid");
        string finalPath = Path.Combine(UserVocalDirectory(), LaneFileKey(lane.Id) + ".wav");
        try
        {
            if (progress is not null) await progress("Vocals · Preparing vocal MIDI…").ConfigureAwait(false);
            await File.WriteAllBytesAsync(midiPath, ExportLane(project, lane.Id), token).ConfigureAwait(false);

            var voiceStore = new VoiceLibraryStore();
            var savedVoice = voiceStore.Get(voiceId);
            string referenceWav = voiceStore.SamplePath(savedVoice);
            if (savedVoice.PitchProfile?.IsValid != true)
            {
                if (progress is not null) await progress($"Vocals · Analyzing {savedVoice.Name}'s natural singing register…").ConfigureAwait(false);
                var voiceForAnalysis = savedVoice;
                savedVoice = await Task.Run(() => voiceStore.EnsurePitchProfile(voiceForAnalysis), token).ConfigureAwait(false);
            }
            if (ttsManager is null) throw new InvalidOperationException("The managed Qwen TTS service is not available.");
            if (progress is not null) await progress($"Vocals · Generating a connected source vocal phrase ({savedVoice.Name})…").ConfigureAwait(false);
            string sourceSpeechText = VocalSingingEngine.PrepareSourceSpeechText(text);
            await ttsManager.SynthesizeSavedVoiceWavAsync(sourceSpeechText, savedVoice, referenceWav, speech, token, progress).ConfigureAwait(false);
            voiceStore.Select(savedVoice.Id);

            IReadOnlyList<VocalGuidanceEvent> guidance = lane.VocalGuidance.Count > 0
                ? lane.VocalGuidance
                : DefaultVocalGuidance(lane, project.Tempo);
            if (progress is not null) await progress("Vocals · Shaping speech into singing…").ConfigureAwait(false);
            await new VocalSingingEngine().RenderAsync(new VocalSingingRequest
            {
                InputSpeechWavPath = speech,
                SpeechText = text,
                VocalMidiPath = midiPath,
                OutputSingingWavPath = finalPath,
                Guidance = guidance,
                Options = VocalSingingEngine.OptionsForVoice(savedVoice.PitchProfile)
            }, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (progress is not null) await progress("Vocals · Singing ready.").ConfigureAwait(false);

            return new JsonObject
            {
                ["laneId"] = lane.Id,
                ["wavPath"] = finalPath,
                ["voiceId"] = savedVoice.Id,
                ["voiceName"] = savedVoice.Name,
                ["guidanceCount"] = guidance.Count,
                ["qwenModelType"] = "server/reference-clone",
                ["voiceLibrary"] = voiceStore.ToPayload()
            };
        }
        finally
        {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
        }
    }

    private static IReadOnlyList<VocalGuidanceEvent> DefaultVocalGuidance(Lane lane, int tempo)
    {
        double secondsPerBeat = 60.0 / tempo;
        var notes = lane.Notes.OrderBy(n => n.Start).ThenBy(n => n.Pitch).ToArray();
        var output = new List<VocalGuidanceEvent>(notes.Length);
        for (int i = 0; i < notes.Length; i++)
        {
            var n = notes[i];
            float accent = Math.Clamp((n.Velocity - 96) / 88f, -.35f, .55f);
            output.Add(new VocalGuidanceEvent
            {
                TimeSeconds = n.Start * secondsPerBeat,
                DurationSeconds = n.Duration * secondsPerBeat,
                Accent = accent,
                SlideSeconds = i == 0 ? 0f : .12f,
                MelodyInfluence = 1f,
                RhythmInfluence = 1f,
                VowelHold = 1.25f,
                ConsonantDrive = .7f
            });
        }
        return output;
    }

    public static byte[] ExportLane(SongProject project, string laneId)
    {
        ValidateProject(project);
        var lane = project.Lanes.SingleOrDefault(l => l.Id == laneId) ?? throw new ArgumentException("Unknown lane.");
        if (lane.Notes.Count == 0)
            throw new ArgumentException("This lane has no MIDI notes to drag.");
        string[] meter = project.Meter.Split('/');
        int channel = lane.Drums ? 9 : 0;
        var composition = new ResonatorComposition
        {
            Tempo = project.Tempo,
            Numerator = int.Parse(meter[0]),
            Denominator = int.Parse(meter[1])
        };
        composition.Events.Add(new ControlChangeEvent(0, channel, 0, lane.Drums ? 0 : lane.Bank));
        composition.Events.Add(new ProgramChangeEvent(0, channel, lane.Program));
        foreach (var note in lane.Notes)
        {
            long start = (long)Math.Round(note.Start * composition.Ppq);
            long duration = Math.Max(1, (long)Math.Round(note.Duration * composition.Ppq));
            // A per-lane DAW drag is the musical clip itself; mixer volume is a
            // playback concern and must not rewrite the lane's authored velocities.
            composition.Events.Add(new NoteEvent(start, duration, note.Pitch, note.Velocity, channel, IsPercussion: lane.Drums));
            composition.LengthTicks = Math.Max(composition.LengthTicks, start + duration);
        }
        long clipLength = lane.ClipLengthBeats > 0 ? (long)Math.Round(lane.ClipLengthBeats * composition.Ppq) : 0;
        composition.LengthTicks = Math.Max(composition.LengthTicks, clipLength);
        return MidiFileWriter.Write(composition, new ResonatorProfile());
    }

    public static byte[] Export(SongProject project)
    {
        ValidateProject(project);
        string[] meter = project.Meter.Split('/');
        var song = new ResonatorComposition
        {
            Tempo = project.Tempo,
            Numerator = int.Parse(meter[0]),
            Denominator = int.Parse(meter[1])
        };
        bool solo = project.Lanes.Any(l => l.Solo);
        int channel = 0;
        var tracks = new List<MidiTrackDefinition>();
        foreach (var lane in project.Lanes)
        {
            if (lane.Volume == 0 || lane.Muted || solo && !lane.Solo || lane.Notes.Count == 0)
                continue;
            if (channel == 9)
                channel++;
            int ch = lane.Drums ? 9 : channel++;
            var events = new List<ResonatorEvent>
            {
                new ControlChangeEvent(0, ch, 0, lane.Drums ? 0 : lane.Bank),
                new ProgramChangeEvent(0, ch, lane.Program)
            };
            long trackLength = 0;
            foreach (var n in lane.Notes)
            {
                long start = (long)Math.Round(n.Start * 480), duration = Math.Max(1, (long)Math.Round(n.Duration * 480));
                events.Add(new NoteEvent(start, duration, n.Pitch, Math.Clamp((int)(n.Velocity * lane.Volume), 1, 127), ch, IsPercussion: lane.Drums));
                trackLength = Math.Max(trackLength, start + duration);
            }
            long clipLength = lane.ClipLengthBeats > 0 ? (long)Math.Round(lane.ClipLengthBeats * 480) : 0;
            trackLength = Math.Max(trackLength, clipLength);
            song.LengthTicks = Math.Max(song.LengthTicks, trackLength);
            tracks.Add(new MidiTrackDefinition(lane.Name, trackLength, events));
        }
        if (tracks.Count == 0)
            throw new ArgumentException("There are no audible MIDI lanes to export.");

        long bar = int.Parse(meter[0]) * 1920 / int.Parse(meter[1]);
        song.LengthTicks = (song.LengthTicks + bar - 1) / bar * bar;
        tracks = tracks.Select(t => t with { LengthTicks = Math.Max(t.LengthTicks, song.LengthTicks) }).ToList();
        return MidiFileWriter.WriteFormat1(song, tracks, new ResonatorProfile());
    }

    public static void ValidateProject(SongProject p)
    {
        if (p.Tempo is < 30 or > 240 || p.Bars is < 1 or > 64 || !ResonatorNotationValidator.IsMeter(p.Meter))
            throw new ArgumentException("Invalid tempo, bars or meter.");
        if (p.Lanes.Count is < 1 or > 16 || p.Lanes.Select(l => l.Id).Distinct().Count() != p.Lanes.Count || p.Lanes.Count(l => !l.Drums) > 15 || p.Lanes.Count(l => l.Drums) > 1)
            throw new ArgumentException("Use up to 15 melodic lanes and one drum lane, with unique IDs.");
        foreach (var l in p.Lanes)
        {
            if (l.Program is < 0 or > 127 || l.Bank is < 0 or > 128 || !double.IsFinite(l.Volume) || l.Volume is < 0 or > 1 || l.Notes.Count > 8192 ||
                !double.IsFinite(l.ClipLengthBeats) || l.ClipLengthBeats < 0 || l.ClipLengthBeats > 4096 || l.Drums && l.Vocals ||
                l.Lyrics.Length > 12000 || l.VoiceId.Length > 256 || l.RenderedVocalPath.Length > 4096 || l.VocalGuidance.Count > 8192)
                throw new ArgumentException("Invalid lane.");
            foreach (var g in l.VocalGuidance)
                if (!double.IsFinite(g.TimeSeconds) || g.TimeSeconds < 0 || g.TimeSeconds > 86400 ||
                    g.DurationSeconds is double gd && (!double.IsFinite(gd) || gd <= 0 || gd > 86400) ||
                    !float.IsFinite(g.Accent) || !float.IsFinite(g.SlideSeconds) || !float.IsFinite(g.MelodyInfluence) ||
                    !float.IsFinite(g.RhythmInfluence) || !float.IsFinite(g.VowelHold) || !float.IsFinite(g.ConsonantDrive) ||
                    !float.IsFinite(g.PitchOffsetSemitones))
                    throw new ArgumentException("Invalid vocal guidance event.");
            foreach (var n in l.Notes)
                if (!double.IsFinite(n.Start) || !double.IsFinite(n.Duration) || n.Start < 0 || n.Duration <= 0 || n.Start + n.Duration > 4096 || n.Pitch is < 0 or > 127 || n.Velocity is < 1 or > 127)
                    throw new ArgumentException("Invalid note.");
        }
    }
}
