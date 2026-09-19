namespace Wds.Resone.Api.VocalSinging;

public sealed record VoicePreviewMelody(string Id, string Name, string Feeling, string Notation);

public static class VoicePreviewMelodies
{
    // Six pitched events for the six sung syllables in "We de-vel-op soft-ware".
    // These are deliberately tiny interval studies derived from Resone's interval-emotion guide.
    public static readonly IReadOnlyList<VoicePreviewMelody> All = new[]
    {
        new VoicePreviewMelody("fun", "Fun", "playful lift and bounce", "tempo=112 4/4 key=C | C4 E4 G4 A4 / | G4 E4 _ _ /"),
        new VoicePreviewMelody("amazing", "Amazing", "wide, surprised, radiant", "tempo=108 4/4 key=C | C4 G4 A4 E5 / | D5 C5 _ _ /"),
        new VoicePreviewMelody("strong", "Strong", "open fifths and grounded authority", "tempo=104 4/4 key=A | A3 E4 A4 E4 / | G4 A4 _ _ /"),
        new VoicePreviewMelody("tender", "Tender", "warm thirds and safe descent", "tempo=92 4/4 key=C | C4 E4 G4 E4 / | D4 C4 _ _ /"),
        new VoicePreviewMelody("heroic", "Heroic", "declarative fourths and an expansive sixth", "tempo=110 4/4 key=A | A3 D4 E4 C#5 / | B4 A4 _ _ /"),
        new VoicePreviewMelody("hopeful", "Hopeful", "ascending thirds opening into a sixth", "tempo=100 4/4 key=C | C4 E4 A4 G4 / | E5 D5 _ _ /"),
        new VoicePreviewMelody("playful", "Playful", "light whole-step motion and quick answers", "tempo=124 4/4 key=G | G4 A4 B4 D5 / | B4 A4 _ _ /"),
        new VoicePreviewMelody("dreamy", "Dreamy", "spacious color with a floating major seventh", "tempo=84 4/4 key=C | C4 G4 B4 A4 / | E5 D5 _ _ /"),
        new VoicePreviewMelody("triumphant", "Triumphant", "octave identity enlarged into arrival", "tempo=116 4/4 key=C | C4 G4 C5 E5 / | G5 C5 _ _ /"),
        new VoicePreviewMelody("mysterious", "Mysterious", "tritone hinge and chromatic uncertainty", "tempo=88 4/4 key=A | A3 D#4 E4 A#4 / | E4 C#4 _ _ /"),
        new VoicePreviewMelody("yearning", "Yearning", "minor sixth reach with intimate return", "tempo=86 4/4 key=A | A3 F4 E4 C4 / | E4 A4 _ _ /"),
        new VoicePreviewMelody("serene", "Serene", "held openness and gentle stepwise settling", "tempo=76 4/4 key=F | F4 C5 A4 G4 / | F4 E4 _ _ /"),
        new VoicePreviewMelody("bright", "Bright", "major thirds and clean upward momentum", "tempo=108 4/4 key=D | D4 F#4 A4 B4 / | F#5 E5 _ _ /"),
        new VoicePreviewMelody("dark", "Dark", "minor thirds with a low, weighted return", "tempo=90 4/4 key=Dm | D4 F4 A4 C5 / | A4 D4 _ _ /"),
        new VoicePreviewMelody("epic", "Epic", "fifth, octave, then broad sixth color", "tempo=112 4/4 key=A | A3 E4 A4 F#5 / | E5 A4 _ _ /"),
        new VoicePreviewMelody("warm", "Warm", "major-third reassurance and affectionate descent", "tempo=94 4/4 key=G | G4 B4 D5 B4 / | A4 G4 _ _ /"),
        new VoicePreviewMelody("adventurous", "Adventurous", "rise-and-declare fourths with forward steps", "tempo=118 4/4 key=D | D4 G4 A4 B4 / | E5 D5 _ _ /"),
        new VoicePreviewMelody("celestial", "Celestial", "major-seventh tension resolving into open sky", "tempo=82 4/4 key=C | C4 B4 C5 G5 / | E5 D5 _ _ /"),
        new VoicePreviewMelody("determined", "Determined", "repeated center then earnest minor-third drive", "tempo=106 4/4 key=Am | A3 A3 C4 E4 / | G4 A4 _ _ /"),
        new VoicePreviewMelody("peaceful", "Peaceful", "open fifth and descending sixth release", "tempo=72 4/4 key=C | C4 G4 E5 C5 / | A4 E4 _ _ /")
    };

    public static VoicePreviewMelody Get(string? id)
        => All.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];
}
