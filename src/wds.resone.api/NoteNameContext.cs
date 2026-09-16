using System.Globalization;
using System.Text.Json.Nodes;

namespace Wds.Resone.Api;

/// <summary>LLM-only projection; native playback retains numeric MIDI pitches.</summary>
public static class NoteNameContext
{
    private static readonly string[] Names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    public static string PitchName(int pitch)
    {
        if (pitch is < 0 or > 127) throw new ArgumentOutOfRangeException(nameof(pitch));
        return Names[pitch % 12] + (pitch / 12 - 1).ToString(CultureInfo.InvariantCulture);
    }

    public static JsonArray Create(IEnumerable<Note> notes)
    {
        var result = new JsonArray();
        foreach (var n in notes)
            result.Add((JsonNode)new JsonObject {
                ["pitch"] = PitchName(n.Pitch), ["start"] = n.Start,
                ["duration"] = n.Duration, ["velocity"] = n.Velocity
            });
        return result;
    }
}
