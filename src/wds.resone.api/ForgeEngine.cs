using System.Text.Json;
using System.Text.Json.Nodes;
using Wds.Resone.Api.Ai;
using Wds.Resone.Api.Music;
using Wds.Resone.Resonator;

namespace Wds.Resone.Api;
/// <summary>Composition root: music AI -> validated notation -> Resonator -> explicit note timeline.</summary>
public sealed class ForgeEngine(ResoneSettings settings, string assetsRoot, HttpClient http)
{
    public async Task<JsonObject> ComposeAsync(JsonElement payload, CancellationToken token)
    {
        var project = payload.GetProperty("project").Deserialize(ResoneJson.Default.SongProject) ?? throw new ArgumentException("Missing project.");
        ValidateProject(project);
        string laneId = payload.GetProperty("laneId").GetString() ?? "";
        if (!project.Lanes.Any(l => l.Id == laneId)) throw new ArgumentException("Select a lane to generate.");
        string description = payload.GetProperty("description").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(description) || description.Length > 12000)
            throw new ArgumentException("Describe the music in 1–12000 characters.");
        bool useAhd = !payload.TryGetProperty("useAhd", out var ahd) || ahd.GetBoolean();
        return await new ArrangementComposer(http, settings, assetsRoot).ComposeAsync(project, laneId, description, useAhd, token);
    }

    public static byte[] Export(SongProject project)
    {
        ValidateProject(project);
        string[] meter = project.Meter.Split('/');
        var c = new ResonatorComposition
        {
            Tempo = project.Tempo,
            Numerator = int.Parse(meter[0]),
            Denominator = int.Parse(meter[1])
        };
        bool solo = project.Lanes.Any(l => l.Solo);
        int channel = 0;
        foreach (var lane in project.Lanes)
        {
            if (lane.Volume == 0 || lane.Muted || solo && !lane.Solo)
                continue;
            if (channel == 9)
                channel++;
            int ch = lane.Drums ? 9 : channel++;
            c.Events.Add(new ControlChangeEvent(0, ch, 0, lane.Drums ? 0 : lane.Bank));
            c.Events.Add(new ProgramChangeEvent(0, ch, lane.Program));
            foreach (var n in lane.Notes)
            {
                long start = (long)Math.Round(n.Start * 480), duration = Math.Max(1, (long)Math.Round(n.Duration * 480));
                c.Events.Add(new NoteEvent(start, duration, n.Pitch, Math.Clamp((int)(n.Velocity * lane.Volume), 1, 127), ch, IsPercussion: lane.Drums));
                c.LengthTicks = Math.Max(c.LengthTicks, start + duration);
            }
        }

        long bar = int.Parse(meter[0]) * 1920 / int.Parse(meter[1]);
        c.LengthTicks = (c.LengthTicks + bar - 1) / bar * bar;
        return MidiFileWriter.Write(c, new ResonatorProfile());
    }

    public static void ValidateProject(SongProject p)
    {
        if (p.Tempo is < 30 or > 240 || p.Bars is < 1 or > 64 || !ResonatorNotationValidator.IsMeter(p.Meter))
            throw new ArgumentException("Invalid tempo, bars or meter.");
        if (p.Lanes.Count is < 1 or > 16 || p.Lanes.Select(l => l.Id).Distinct().Count() != p.Lanes.Count || p.Lanes.Count(l => !l.Drums) > 15 || p.Lanes.Count(l => l.Drums) > 1)
            throw new ArgumentException("Use up to 15 melodic lanes and one drum lane, with unique IDs.");
        foreach (var l in p.Lanes)
        {
            if (l.Program is < 0 or > 127 || l.Bank is < 0 or > 128 || !double.IsFinite(l.Volume) || l.Volume is < 0 or > 1 || l.Notes.Count > 8192)
                throw new ArgumentException("Invalid lane.");
            foreach (var n in l.Notes)
                if (!double.IsFinite(n.Start) || !double.IsFinite(n.Duration) || n.Start < 0 || n.Duration <= 0 || n.Start + n.Duration > 4096 || n.Pitch is < 0 or > 127 || n.Velocity is < 1 or > 127)
                    throw new ArgumentException("Invalid note.");
        }
    }
}
