namespace Wds.Resone.Api.VocalSinging;

public sealed record VocalGuidanceEvent
{
    public double TimeSeconds { get; init; }
    public double? DurationSeconds { get; init; }
    public float MelodyInfluence { get; init; } = 1f;
    public float RhythmInfluence { get; init; } = 1f;
    public float Accent { get; init; } = 0f;
    public float SlideSeconds { get; init; } = 0f;
    public float PitchOffsetSemitones { get; init; } = 0f;
    public float VowelHold { get; init; } = 1f;
    public float ConsonantDrive { get; init; } = 0.65f;
}

public sealed record VocalSingingOptions
{
    public float PitchCorrectionStrength { get; init; } = 1f;
    public float FormantPreserve { get; init; } = 0.8f;
    public float VibratoDepthCents { get; init; } = 18f;
    public float VibratoRateHz { get; init; } = 5.2f;
    public float OutputGain { get; init; } = 0.95f;
    public int MinPitchHz { get; init; } = 50;
    public int MaxPitchHz { get; init; } = 800;
    // When set, melody notes are octave-folded into the analyzed comfortable
    // register before pitch correction. -1 disables register restriction.
    public int BaseMidiNote { get; init; } = -1;
    public int SingingMinMidiNote { get; init; } = -1;
    public int SingingMaxMidiNote { get; init; } = -1;
}

public sealed record VocalSingingRequest
{
    public required string InputSpeechWavPath { get; init; }
    public required string SpeechText { get; init; }
    public required string VocalMidiPath { get; init; }
    public required string OutputSingingWavPath { get; init; }
    public IReadOnlyList<VocalGuidanceEvent> Guidance { get; init; } = Array.Empty<VocalGuidanceEvent>();
    public VocalSingingOptions Options { get; init; } = new();
}
