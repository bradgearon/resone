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

    public Task<JsonObject> ComposeAsync(SongProject project, string laneId, string description, bool useAhd, CancellationToken token, Func<string, Task>? progress = null)
        => ComposeCoreAsync(project, laneId, description, useAhd, null, token, progress);

    /// <summary>
    /// Full-song-only path. It uses the same proven lane arranger but gives it provisioned long-form context and
    /// allows one plain-text memory block after the notation. The regular ComposeAsync path never sees this contract.
    /// </summary>
    public Task<JsonObject> ComposeSongChunkAsync(SongProject project, string laneId, string description, bool useAhd, SongGenerationContext songContext, CancellationToken token, Func<string, Task>? progress = null)
        => ComposeCoreAsync(project, laneId, description, useAhd, songContext, token, progress);

    private async Task<JsonObject> ComposeCoreAsync(SongProject project, string laneId, string description, bool useAhd, SongGenerationContext? songContext, CancellationToken token, Func<string, Task>? progress)
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
        string intervalGuide = InstructionContent.Read(assetsRoot, "interval_emotion_field_guide.md");
        if (progress is not null) await progress(songContext is null ? "Composing · Directing…" : "Song · Directing section…").ConfigureAwait(false);
        string narrative = await MusicNarrativePlanner.CreateAsync(
            client, description, intervalGuide, useAhd, project.Bars, project.Tempo, project.Meter,
            target.Notation, target.OriginalBrief, target.Prompts, LaneLength(target), songContext?.Packet ?? "", token).ConfigureAwait(false);
        string composerRequest = MusicNarrativePlanner.AppendNarrativePlan(request.ToJsonString(), narrative);
        if (songContext is not null)
            composerRequest += "\n\n" + songContext.Packet;
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
            var result = Decode(notationText,targetProject,description);
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

    public static JsonObject Decode(string text, SongProject project, string description)
    {
        var headers = Regex.Matches(text, @"(?m)^[ \t]*track=(?<id>[^\s|]+)[ \t]*");
        if (headers.Count == 0)
            throw new ArgumentException("Begin each track with track=<requested laneId> followed by its notation; do not return JSON.");
        var seen=new HashSet<string>();
        var output=new JsonArray();
        var warnings=new JsonArray();
        double length=0;
        for (int i=0;i<headers.Count;i++)
        {
            var header = headers[i];
            string id=header.Groups["id"].Value;
            var lane=project.Lanes.SingleOrDefault(l=>l.Id==id);
            if (lane == null || seen.Contains(id))
            {
                warnings.Add((JsonNode?)JsonValue.Create("Skipped unknown or duplicate track: " + id));
                continue;
            }
            int start=header.Index+header.Length;
            int end=i+1<headers.Count?headers[i+1].Index:text.Length;
            string notation=RequestedTiming.Apply(text[start..end].Trim(), project.Tempo, project.Meter);
            try
            {
            if(notation.Length is 0 or >65536) throw new ArgumentException("Invalid notation length for "+lane.Name);
            var profile=new ResonatorProfile();
            if(lane.Drums) foreach(var d in Drums) profile.Percussion.NoteMap[d.Note]=d.Pitch;
            var generated=new ResonatorMidiGenerator().Generate(notation,profile);
            var c=generated.Composition;
            foreach (var warning in generated.Warnings)
                warnings.Add((JsonNode?)JsonValue.Create("Recovered " + lane.Name + ": " + warning));

            var notes=c.Events.OfType<NoteEvent>().Select(n=>new Note(n.Tick/(double)c.Ppq,n.DurationTicks/(double)c.Ppq,n.MidiNote,n.Velocity)).ToList();
            if(notes.Count is 0 or >8192 || notes.Any(n=>n.Start<0 || n.Duration<=0 || n.Start+n.Duration>4096))
                throw new ArgumentException("Invalid or empty notes for "+lane.Name);
            seen.Add(id);
            length=Math.Max(length,c.LengthTicks/(double)c.Ppq);
            output.Add((JsonNode)new JsonObject { ["laneId"]=id,["notation"]=notation,
                ["originalBrief"]=string.IsNullOrEmpty(lane.OriginalBrief)?description:lane.OriginalBrief,
                ["notes"]=JsonSerializer.SerializeToNode(notes,ResoneJson.Default.ListNote) });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException or ResonatorParseException)
            {
                warnings.Add((JsonNode?)JsonValue.Create("Skipped " + lane.Name + ": " + ex.Message));
            }
        }
        if (output.Count == 0)
            throw new ArgumentException("No playable tracks. " + warnings.ToJsonString());
        var meter=project.Meter.Split('/');
        return new JsonObject { ["tracks"]=output,["warnings"]=warnings,["lengthBeats"]=length,
            ["bars"]=(int)Math.Ceiling(length/(int.Parse(meter[0])*4.0/int.Parse(meter[1]))) };
    }
}
