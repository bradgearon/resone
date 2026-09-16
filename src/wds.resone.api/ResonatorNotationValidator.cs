using System.Text;
using System.Text.RegularExpressions;
using Wds.Resone.Resonator;

namespace Wds.Resone.Api.Music;

/// <summary>The executable Resonator parser owns notation syntax.</summary>
public static class ResonatorNotationValidator
{
    public static bool IsMeter(string meter) => meter != null &&
        Regex.IsMatch(meter, @"^(?:[1-9]|1[0-2])/(?:2|4|8|16)$");

    public static IReadOnlyList<string> Validate(string notation, MusicCompositionRequest request)
    {
        if (string.IsNullOrWhiteSpace(notation) || notation.Length > 65536)
            return new[] { "Notation must contain 1–65536 characters." };
        if (!IsMeter(request.Meter))
            return new[] { "Invalid requested meter." };
        try
        {
            var generated = new ResonatorMidiGenerator().Generate(notation);
            var c = generated.Composition;
            var errors = new List<string>();
            if (c.Tempo != request.Tempo) errors.Add("Tempo differs from request.");
            if ($"{c.Numerator}/{c.Denominator}" != request.Meter)
                errors.Add("Meter differs from request.");
            if (!c.Events.OfType<NoteEvent>().Any()) errors.Add("Score must contain notes or chords.");
            return errors;
        }
        catch (Exception ex) when (ex is ResonatorParseException or ArgumentException or FormatException or OverflowException)
        {
            return new[] { ex.Message };
        }
    }

    public static string CompleteFinalMeasure(string notation, MusicCompositionRequest request, out int actualBars)
    {
        var errors = Validate(notation, request);
        if (errors.Count != 0) throw new ArgumentException(string.Join("\n", errors));
        var c = new ResonatorParser(new ResonatorProfile()).Parse(notation).Composition;
        // Count the parser's grid cursor, not gate/legato tails; repetition and
        // named chords are already expanded by the canonical parser.
        long bar = c.Numerator * c.Ppq * 4L / c.Denominator;
        actualBars = checked((int)((c.NominalLengthTicks + bar - 1) / bar));
        long padding = actualBars * bar - c.NominalLengthTicks;
        var result = new StringBuilder(notation.Trim());
        foreach (var (ticks, rest) in new[] { (c.Ppq, " _"), (c.Ppq / 2, " _,"), (c.Ppq / 4, " _.") })
            while (padding >= ticks) { result.Append(rest); padding -= ticks; }
        if (result.Length > 65536) throw new ArgumentException("Notation plus rests exceeds 65536 characters.");
        return result.ToString();
    }
}
