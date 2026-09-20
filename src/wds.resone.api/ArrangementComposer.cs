using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Wds.Resone.Api.Ai;
using Wds.Resone.Api.Music;
using Wds.Resone.Resonator;

namespace Wds.Resone.Api;

/// <summary>One coordinated model request, validated and applied as a whole arrangement.</summary>
public sealed class ArrangementComposer(HttpClient http, ResoneSettings settings, string assetsRoot)
{
    private static readonly (string Note, int Pitch, string Name)[] Drums = [
        ("C2",36,"kick"), ("D2",38,"snare"), ("F#2",42,"closed hat"),
        ("A#2",46,"open hat"), ("C#3",49,"crash"), ("D#3",51,"ride"),
        ("F2",41,"low tom"), ("A2",45,"mid tom"), ("D3",50,"high tom") ];

    public Task<JsonObject> ComposeAsync(SongProject project, string laneId, string description, bool useAhd, string composerOverview, CancellationToken token, Func<string, Task>? progress = null)
        => ComposeCoreAsync(project, laneId, description, useAhd, null, composerOverview, token, progress);

    /// <summary>
    /// Full-song-only path. It uses the same proven lane arranger but gives it provisioned long-form context and
    /// allows one plain-text memory block after the notation. The regular ComposeAsync path never sees this contract.
    /// </summary>
    public Task<JsonObject> ComposeSongChunkAsync(SongProject project, string laneId, string description, bool useAhd, SongGenerationContext songContext, CancellationToken token, Func<string, Task>? progress = null)
        => ComposeCoreAsync(project, laneId, description, useAhd, songContext, "", token, progress);

    private async Task<JsonObject> ComposeCoreAsync(SongProject project, string laneId, string description, bool useAhd, SongGenerationContext? songContext, string composerOverview, CancellationToken token, Func<string, Task>? progress)
    {
        var instructions = MusicCompositionInstructions.Load(assetsRoot);
        if (string.IsNullOrWhiteSpace(instructions.ArrangementInstructions))
            throw new InvalidDataException("music-composition.json requires arrangementInstructions. Deploy the updated instruction file.");
        var target = project.Lanes.Single(l => l.Id == laneId);
        var compositionTips = InstructionContent.Read(assetsRoot, "composition-tips.md");
        // The request supplies the actual drum map only when composing percussion.
        var references = Regex.Replace(instructions.References,
            @"(?ms)^## Percussion\s.*?(?=^## |\z)", "");
        // The silent narrative planner already consumed the interval-emotion field guide and
        // turned it into concrete interval/perspective requirements. Do not make the composer
        // re-plan the emotions from the full guide; it only needs AHD + executable notation refs.
        references = Regex.Replace(references,
            @"(?ms)\nREFERENCE: interval_emotion_field_guide\.md\s.*?(?=\nREFERENCE:|\z)", "\n");
        string prompt = instructions.SystemPrompt + "\n" + references + "\n" + compositionTips
            + "\n" + instructions.ArrangementInstructions;
        if (target.Drums)
            prompt += "\nSelected lane is percussion. Use mode=drums and only these explicit drum notes: "
                + string.Join(", ", Drums.Select(d => $"{d.Note}={d.Name}"))
                + ". Compose a style-appropriate groove with rests, subdivisions and accents. "
                + "IMPORTANT: square-bracket chord syntax means SIMULTANEOUS DRUM HITS and should be used whenever drums hit together. "
                + "For example [C2 F#2] is kick + closed hat at the same instant, [D2 F#2] is snare + closed hat, and [C2 D2 C#3] layers kick + snare + crash. "
                + "A bracketed drum chord consumes one rhythmic event/time slot; duration, velocity, gate, offset, and accent modifiers apply to the whole bracketed hit.";
        else if (target.Vocals)
            prompt += "\nSelected lane is a VOCAL MELODY. Generate monophonic, singable pitched Resonator notation only; do not emit lyrics, words, phonemes, or mode=drums. The lyrics/text and voice are rendered separately after the MIDI exists. Keep phrases human-singable, leave breathing space, avoid impossible overlapping vocal notes, and use rhythm/duration as intentional syllable and vowel timing.";
        else
            prompt += "\nSelected lane is pitched. Do not emit mode=drums.";

        if (!string.IsNullOrWhiteSpace(composerOverview))
        {
            prompt += """

EXISTING SONG COMPOSER OVERVIEW — PERSISTENT MUSICAL DNA.
This piece was created from the composer overview supplied below. The user's new request is the requested modification, but preserve the overview's established tonal plan, emotional note palette, melody/motif identities, chord relationships, AHD activations, answers/contrasts/continuations, and other musical DNA unless the new request explicitly asks to change them. Use the overview as context for this revision; do not regenerate it and do not output it.
""";
        }

        if (songContext is not null)
        {
            prompt += """

FULL SONG SECTION MODE — ONLY ACTIVE FOR THIS REQUEST.
You are rendering one lane for one producer-planned section of a larger song. The producer ran once before section generation began and does not participate in individual chunks. Follow the supplied FULL SONG GENERATION CONTEXT and CURRENT SECTION.
Return the selected track first using the normal Resonator contract. AFTER the complete notation, emit a line containing exactly:
SONG MEMORY NOTES
Then emit concise plain text with exactly these labels:
Melody notes: important line-shape, statement/answer/counter relationships, remembered-note positions, register, or melodic facts worth carrying forward. Put exact reusable Resonator fragments in backticks when useful. If this lane establishes no melodic fact, leave this line brief.
Motifs: only important reusable motifs/rhythmic cells. Put exact reusable Resonator fragments in backticks and briefly describe their role.
Important chords: only important chord/progression/harmonic relationships worth remembering. Put exact reusable Resonator fragments in backticks when available.
Next composer notes: the ACTIVE handoff list of musical promises that still need fulfillment later. Include any incoming OPEN COMPOSER COMMITMENTS that this chunk did not fulfill, and add new setups created here (for example a two-chord relationship that must answer/resolve later, a held/divergent pitch that must return, a drum roll that must land on the next section, a motif whose third repetition is intentionally delayed, or a transition that another lane/section must complete). State the intended target lane/section when it matters. Remove an incoming item only if this chunk actually fulfilled it. If no commitments remain, write `None.`
Keep this memory block compact. Do not paste the whole generated track into it. The SONG MEMORY NOTES block is forbidden in ordinary lane generation and exists only in song mode.

HANDOFF DISCIPLINE: Read OPEN COMPOSER COMMITMENTS in the supplied full-song context before composing. A commitment that belongs to another lane or a later section is not fulfilled merely because you saw it; carry it forward in Next composer notes. Each composer call is responsible for preserving unresolved promises so no setup, answer/counter relationship, harmonic expectation, rhythmic pickup, roll, held note, or delayed payoff disappears between chunks.
""";
        }

        static double LaneLength(Lane l)
        {
            double noteLength = l.Notes.Count == 0 ? 0 : l.Notes.Max(n => n.Start + n.Duration);
            double clipLength = l.ClipLengthBeats > 0 && double.IsFinite(l.ClipLengthBeats) ? l.ClipLengthBeats : 0;
            return Math.Max(noteLength, clipLength);
        }
        JsonObject LaneContext(Lane l) => new() {
            ["laneId"]=l.Id, ["name"]=l.Name, ["bank"]=l.Bank,
            ["program"]=l.Program, ["drums"]=l.Drums, ["vocals"]=l.Vocals,
            ["existingNotation"]=l.Notation,
            ["existingLengthBeats"]=LaneLength(l),
            ["noteCount"]=l.Notes.Count,
            ["importedMidiName"]=l.ImportedMidiName,
            ["originalBrief"]=l.OriginalBrief,
            ["prompts"]=new JsonArray(l.Prompts.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()) };
        var context = new JsonArray();
        foreach (var l in project.Lanes.Where(l => l.Id != laneId && (l.Notes.Count > 0 || !string.IsNullOrWhiteSpace(l.Notation))))
            context.Add((JsonNode)LaneContext(l));
        var meter = project.Meter.Split('/');
        var request = new JsonObject { ["description"]=description, ["tempo"]=project.Tempo,
            ["meter"]=project.Meter, ["bars"]=project.Bars,
            ["requestedBeats"]=project.Bars * int.Parse(meter[0]) * 4.0 / int.Parse(meter[1]),
            ["useAhd"]=useAhd, ["selectedLane"]=LaneContext(target), ["contextLanes"]=context };
        // The decoder can only return the selected lane; context is never a write target.
        var targetProject = new SongProject { Tempo=project.Tempo, Meter=project.Meter, Bars=project.Bars, Lanes=[target] };
        ILocalChatModelClient client = settings.LocalInferenceEnabled ? new NativeChatClient(settings) : new LocalAiClient(http,settings);

        string laneGenreContext = "";
        if (songContext is null && target.Drums)
        {
            if (progress is not null) await progress("Composing · Identifying drum genre…").ConfigureAwait(false);
            string genreIdentificationRequest = description;
            if (!string.IsNullOrWhiteSpace(target.OriginalBrief))
                genreIdentificationRequest += "\nExisting drum lane brief: " + target.OriginalBrief;
            if (target.Prompts.Count != 0)
                genreIdentificationRequest += "\nRecent drum update requests: " + string.Join(" -> ", target.Prompts.TakeLast(4));
            string identifiedGenre = await GenreIdentificationPass.IdentifyAsync(client, genreIdentificationRequest, token).ConfigureAwait(false);
            GenreBriefResolver.Selection genre = string.IsNullOrWhiteSpace(identifiedGenre)
                ? GenreBriefResolver.Selection.Empty
                : GenreBriefResolver.Resolve(assetsRoot, identifiedGenre);
            // Fail soft: if the tiny classifier returns an unfamiliar label or NONE, deterministic
            // matching against the original request may still recognize an explicit genre phrase.
            if (string.IsNullOrWhiteSpace(genre.DrumBrief))
                genre = GenreBriefResolver.Resolve(assetsRoot, description);
            laneGenreContext = genre.DrumPromptContext;
        }

        if (!string.IsNullOrWhiteSpace(laneGenreContext))
        {
            prompt += "\n\nDRUM GENRE GUIDANCE — CURRENT LANE ONLY\n"
                + laneGenreContext
                + "\nUse this for the selected drum lane only. Do not output the guide or genre-analysis prose.";
        }

        string intervalGuide = InstructionContent.Read(assetsRoot, "interval_emotion_field_guide.md");
        if (progress is not null) await progress(songContext is null ? "Composing · Directing…" : "Song · Directing section…").ConfigureAwait(false);
        string narrative = await MusicNarrativePlanner.CreateAsync(
            client, description, intervalGuide, useAhd, project.Bars, project.Tempo, project.Meter,
            target.Notation, target.OriginalBrief, target.Prompts, LaneLength(target), songContext?.Packet ?? "", composerOverview, laneGenreContext, token).ConfigureAwait(false);
        string composerRequest = MusicNarrativePlanner.AppendNarrativePlan(request.ToJsonString(), narrative);
        if (songContext is not null)
            composerRequest += "\n\n" + songContext.Packet;
        if (!string.IsNullOrWhiteSpace(composerOverview))
            composerRequest += "\n\nEXISTING SONG COMPOSER OVERVIEW — PRESERVE UNLESS THE USER EXPLICITLY CHANGES IT\n" + composerOverview;
        var messages = new List<ChatMessage> { new("system",prompt), new("user",composerRequest) };
        if (progress is not null) await progress(songContext is null ? "Composing · Arranging…" : "Song · Arranging section…").ConfigureAwait(false);
        var text = await client.CompleteTextStreamingAsync(messages, songContext is null ? "MusicArrangement" : "SongChunkArrangement", null, token);
        token.ThrowIfCancellationRequested();
        if (progress is not null) await progress(songContext is null ? "Composing · Rendering MIDI…" : "Song · Rendering MIDI…").ConfigureAwait(false);

        string notationText = text;
        string memoryNotes = "";
        if (songContext is not null)
        {
            var marker = Regex.Match(text, @"(?m)^\s*SONG MEMORY NOTES\s*$", RegexOptions.IgnoreCase);
            if (marker.Success)
            {
                notationText = text[..marker.Index].TrimEnd();
                memoryNotes = text[(marker.Index + marker.Length)..].Trim();
            }
        }

        try
        {
            var result = Decode(notationText,targetProject,description, allowEmptyTracks: songContext is not null);
            if (songContext is not null)
            {
                result["songMemoryNotes"] = memoryNotes;
                result["songNarrativePlan"] = narrative;
                result["songSectionId"] = songContext.SectionId;
            }
            return result;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException or ResonatorParseException)
        {
            throw new InvalidDataException("Invalid arrangement: " + ex.Message, ex);
        }
    }

    public static JsonObject Decode(string text, SongProject project, string description, bool allowEmptyTracks = false)
    {
        var headers = Regex.Matches(text, @"(?m)^[ \t]*track=(?<id>[^\s|]+)[ \t]*");
        var seen=new HashSet<string>();
        var output=new JsonArray();
        var warnings=new JsonArray();
        double length=0;

        void AddRecoveredTrack(Lane lane, string notation, GenerateResult generated)
        {
            var c=generated.Composition;
            foreach (var warning in generated.Warnings)
                warnings.Add((JsonNode?)JsonValue.Create("Recovered " + lane.Name + ": " + warning));

            var notes=c.Events.OfType<NoteEvent>()
                .Select(n=>new Note(n.Tick/(double)c.Ppq,n.DurationTicks/(double)c.Ppq,n.MidiNote,n.Velocity))
                .Where(n=>n.Start>=0 && n.Duration>0 && n.Start+n.Duration<=4096)
                .Take(8192)
                .ToList();
            if(notes.Count==0)
                throw new ArgumentException("No playable notes remained for "+lane.Name);
            seen.Add(lane.Id);
            length=Math.Max(length,c.LengthTicks/(double)c.Ppq);
            output.Add((JsonNode)new JsonObject { ["laneId"]=lane.Id,["notation"]=notation,
                ["originalBrief"]=string.IsNullOrEmpty(lane.OriginalBrief)?description:lane.OriginalBrief,
                ["notes"]=JsonSerializer.SerializeToNode(notes,ResoneJson.Default.ListNote) });
        }

        bool TryDecodeNotation(Lane lane, string rawNotation, out string error)
        {
            try
            {
                string notation=RequestedTiming.Apply(rawNotation.Trim(), project.Tempo, project.Meter);
                if(notation.Length is 0 or >65536) throw new ArgumentException("Invalid notation length for "+lane.Name);
                var profile=new ResonatorProfile();
                if(lane.Drums) foreach(var d in Drums) profile.Percussion.NoteMap[d.Note]=d.Pitch;
                var generated=new ResonatorMidiGenerator().Generate(notation,profile);
                AddRecoveredTrack(lane,notation,generated);
                error="";
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException or ResonatorParseException)
            {
                error=ex.Message;
                return false;
            }
        }

        static string ExtractPlayableFallback(string raw)
        {
            // Last-resort salvage for structurally malformed model output. The normal
            // Resonator parser is already tolerant at token level; this only runs when
            // the response as a whole still produced zero playable notes. Preserve the
            // order of obvious explicit pitches/chords and basic rests, sacrificing
            // advanced modifiers rather than throwing away the entire section.
            var matches=Regex.Matches(raw,
                @"(?ix)(?:\[(?:\s*[A-G](?:\#|b)?-?\d\s*)+\](?:,|\.|::?)?|(?<![A-Za-z0-9])[A-G](?:\#|b)?-?\d(?:,|\.|::?)?(?![A-Za-z0-9])|(?<!\S)_(?:,|\.|::?)?(?!\S))");
            return string.Join(" ",matches.Select(m=>m.Value.Trim()).Where(x=>x.Length>0));
        }

        void DecodeTrack(Lane lane, string rawNotation, string? recoveryWarning = null)
        {
            if (!string.IsNullOrWhiteSpace(recoveryWarning))
                warnings.Add((JsonNode?)JsonValue.Create(recoveryWarning));

            if (TryDecodeNotation(lane,rawNotation,out var primaryError))
                return;

            warnings.Add((JsonNode?)JsonValue.Create("Primary " + lane.Name + " notation could not be used: " + primaryError));
            string fallback=ExtractPlayableFallback(rawNotation);
            if (!string.IsNullOrWhiteSpace(fallback) && TryDecodeNotation(lane,fallback,out var fallbackError))
            {
                warnings.Add((JsonNode?)JsonValue.Create("Recovered " + lane.Name + " from explicit playable note/chord tokens in malformed composer output."));
                return;
            }

            if (!string.IsNullOrWhiteSpace(fallback))
                warnings.Add((JsonNode?)JsonValue.Create("Fallback " + lane.Name + " notation still contained no playable MIDI: " + fallbackError));

            if (allowEmptyTracks)
            {
                // A song section is part of the producer plan even when one composer
                // response is unusable. Return an explicit empty track so the UI can
                // preserve any existing material (or silence for a new section) and
                // continue through every remaining planned section/lane.
                seen.Add(lane.Id);
                warnings.Add((JsonNode?)JsonValue.Create("No playable MIDI could be recovered for " + lane.Name + "; preserved the section as existing music/silence and continued."));
                output.Add((JsonNode)new JsonObject {
                    ["laneId"]=lane.Id,
                    ["notation"]="",
                    ["originalBrief"]=string.IsNullOrEmpty(lane.OriginalBrief)?description:lane.OriginalBrief,
                    ["notes"]=JsonSerializer.SerializeToNode(new List<Note>(),ResoneJson.Default.ListNote)
                });
            }
        }

        // Arrangement requests currently target exactly one lane. If the model gives us playable
        // Resonator notation but forgets the track=<laneId> wrapper, keep the music instead of
        // throwing the entire section away.
        if (headers.Count == 0)
        {
            if (project.Lanes.Count == 1)
                DecodeTrack(project.Lanes[0], text, $"Recovered {project.Lanes[0].Name}: missing track header; used the only requested lane.");
            else
                throw new ArgumentException("Begin each track with track=<requested laneId> followed by its notation; do not return JSON.");
        }
        else
        {
            for (int i=0;i<headers.Count;i++)
            {
                var header = headers[i];
                string id=header.Groups["id"].Value;
                int start=header.Index+header.Length;
                int end=i+1<headers.Count?headers[i+1].Index:text.Length;
                string rawNotation=text[start..end];
                var lane=project.Lanes.SingleOrDefault(l=>l.Id==id);

                if (lane == null && project.Lanes.Count == 1 && headers.Count == 1)
                {
                    lane = project.Lanes[0];
                    DecodeTrack(lane, rawNotation, $"Recovered {lane.Name}: model returned track={id}; used requested laneId {lane.Id}.");
                    continue;
                }
                if (lane == null || seen.Contains(id))
                {
                    warnings.Add((JsonNode?)JsonValue.Create("Ignored unknown or duplicate track wrapper: " + id));
                    continue;
                }
                DecodeTrack(lane, rawNotation);
            }
        }

        if (output.Count == 0 && allowEmptyTracks)
        {
            foreach (var lane in project.Lanes.Where(l=>!seen.Contains(l.Id)))
            {
                seen.Add(lane.Id);
                warnings.Add((JsonNode?)JsonValue.Create("Composer output contained no usable track wrapper or playable MIDI for " + lane.Name + "; preserved existing music/silence and continued."));
                output.Add((JsonNode)new JsonObject {
                    ["laneId"]=lane.Id,
                    ["notation"]="",
                    ["originalBrief"]=string.IsNullOrEmpty(lane.OriginalBrief)?description:lane.OriginalBrief,
                    ["notes"]=JsonSerializer.SerializeToNode(new List<Note>(),ResoneJson.Default.ListNote)
                });
            }
        }
        if (warnings.Count > 0)
            ResoneDailyLog.WriteBlock("MIDI", "Arrangement recovery warnings", warnings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        if (output.Count == 0)
            throw new ArgumentException("No playable tracks could be recovered. " + warnings.ToJsonString());
        var meter=project.Meter.Split('/');
        return new JsonObject { ["tracks"]=output,["warnings"]=warnings,["lengthBeats"]=length,
            ["bars"]=(int)Math.Ceiling(length/(int.Parse(meter[0])*4.0/int.Parse(meter[1]))) };
    }
}
