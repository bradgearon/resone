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

    public async Task<JsonObject> ComposeAsync(SongProject project, string laneId, string description, bool useAhd, CancellationToken token)
    {
        var instructions = MusicCompositionInstructions.Load(assetsRoot);
        if (string.IsNullOrWhiteSpace(instructions.ArrangementInstructions) ||
            string.IsNullOrWhiteSpace(instructions.ArrangementRepairInstructions))
            throw new InvalidDataException("music-composition.json requires arrangementInstructions and arrangementRepairInstructions. Deploy the updated instruction file.");
        var target = project.Lanes.Single(l => l.Id == laneId);
        var compositionTips = InstructionContent.Read(assetsRoot, "composition-tips.md");
        // The request supplies the actual drum map only when composing percussion.
        var references = Regex.Replace(instructions.References,
            @"(?ms)^## Percussion\s.*?(?=^## |\z)", "");
        string prompt = instructions.SystemPrompt + "\n" + references + "\n" + compositionTips
            + "\n" + instructions.ArrangementInstructions;
        if (target.Drums)
            prompt += "\nSelected lane is percussion. Use mode=drums and only these explicit drum notes: "
                + string.Join(", ", Drums.Select(d => $"{d.Note}={d.Name}"))
                + ". Compose a style-appropriate groove with rests, subdivisions and accents.";
        else
            prompt += "\nSelected lane is pitched. Do not emit mode=drums.";
        JsonObject LaneContext(Lane l) => new() {
            ["laneId"]=l.Id, ["name"]=l.Name, ["bank"]=l.Bank,
            ["program"]=l.Program, ["drums"]=l.Drums,
            ["existingNotation"]=l.Notation,
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
        var messages = new List<ChatMessage> { new("system",prompt), new("user",request.ToJsonString()) };
        ILocalChatModelClient client = settings.NativeInference ? new NativeChatClient(settings) : new LocalAiClient(http,settings);
        string error = "";
        for (int attempt=0;attempt<2;attempt++)
        {
            var text = await client.CompleteTextStreamingAsync(messages, 16384,
                attempt==0 ? "MusicArrangement" : "MusicArrangement.Repair", null, token);
            token.ThrowIfCancellationRequested();
            try { return Decode(text,targetProject,description); }
            catch (Exception ex) when (ex is KeyNotFoundException or JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException or ResonatorParseException)
            { error=ex.Message; }
            messages.Add(new("assistant",text));
            messages.Add(new("user",instructions.ArrangementRepairInstructions.Replace("{{error}}", error)));
        }
        throw new InvalidDataException("Invalid arrangement after one repair attempt: "+error);
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
            var c=new ResonatorMidiGenerator().Generate(notation,profile).Composition;

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
