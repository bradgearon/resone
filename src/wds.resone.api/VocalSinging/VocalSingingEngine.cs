using System.Text;

namespace Wds.Resone.Api.VocalSinging;

public sealed class VocalSingingEngine
{
    public static string PrepareSourceSpeechText(string lyrics)
    {
        if (string.IsNullOrWhiteSpace(lyrics)) return "";
        var words = new List<string>();
        var current = new StringBuilder();
        static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c >= 0x80;
        void Flush()
        {
            if (current.Length == 0) return;
            words.Add(current.ToString());
            current.Clear();
        }
        for (int i = 0; i < lyrics.Length; i++)
        {
            char c = lyrics[i];
            bool joiner = (c is '\'' or '-') && current.Length > 0 && i + 1 < lyrics.Length && IsWordChar(lyrics[i + 1]);
            if (IsWordChar(c) || joiner) current.Append(c);
            else Flush();
        }
        Flush();
        if (words.Count == 0) return lyrics.Trim();
        // Generate one fluent source phrase instead of isolated "word. word." speech.
        // The native aligner already finds monotonic word boundaries, so preserving
        // normal coarticulation gives the singer connected consonants/vowels to work
        // with and avoids resetting the voice into a fresh spoken posture every word.
        var normalized = new StringBuilder();
        bool pendingSpace = false;
        foreach (char c in lyrics.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = normalized.Length > 0;
                continue;
            }
            if (pendingSpace) normalized.Append(' ');
            normalized.Append(c);
            pendingSpace = false;
        }
        return normalized.ToString();
    }

    public static VoicePitchProfile AnalyzeVoiceProfile(string wavPath, int minPitchHz = 50, int maxPitchHz = 800)
    {
        if (!File.Exists(wavPath)) throw new FileNotFoundException("Voice WAV not found", wavPath);
        var error = new StringBuilder(2048);
        int rc = NativeMethods.resone_analyze_voice_profile(
            wavPath, Math.Clamp(minPitchHz, 40, 400), Math.Clamp(maxPitchHz, Math.Max(80, minPitchHz + 20), 1400),
            out var p, error, error.Capacity);
        if (rc != 0) throw new InvalidOperationException($"Voice range analysis failed ({rc}): {error}");
        return new VoicePitchProfile
        {
            ProfileVersion = VoicePitchProfile.CurrentVersion,
            BasePitchHz = p.BasePitchHz, LowPitchHz = p.LowPitchHz, HighPitchHz = p.HighPitchHz, VoicedFraction = p.VoicedFraction,
            BaseMidiNote = p.BaseMidiNote, BaseOctave = p.BaseOctave, SingingMinMidiNote = p.SingingMinMidiNote, SingingMaxMidiNote = p.SingingMaxMidiNote
        };
    }

    public static VocalSingingOptions OptionsForVoice(VoicePitchProfile? profile, VocalSingingOptions? basis = null)
    {
        basis ??= new VocalSingingOptions();
        if (profile?.IsValid != true) return basis;
        return basis with
        {
            BaseMidiNote = profile.BaseMidiNote,
            SingingMinMidiNote = profile.SingingMinMidiNote,
            SingingMaxMidiNote = profile.SingingMaxMidiNote
        };
    }

    public Task<string> RenderAsync(VocalSingingRequest request, CancellationToken cancellationToken = default)
        => Task.Run(() => Render(request), cancellationToken);

    public string Render(VocalSingingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!File.Exists(request.InputSpeechWavPath)) throw new FileNotFoundException("Speech WAV not found", request.InputSpeechWavPath);
        if (!File.Exists(request.VocalMidiPath)) throw new FileNotFoundException("Vocal MIDI not found", request.VocalMidiPath);
        if (string.IsNullOrWhiteSpace(request.SpeechText)) throw new ArgumentException("Speech text is required", nameof(request));

        var nativeGuidance = request.Guidance.Select(x => new NativeMethods.NativeGuidanceEvent
        {
            TimeSeconds = x.TimeSeconds,
            DurationSeconds = x.DurationSeconds ?? 0,
            MelodyInfluence = Math.Clamp(x.MelodyInfluence, 0, 1),
            RhythmInfluence = Math.Clamp(x.RhythmInfluence, 0, 1),
            Accent = Math.Clamp(x.Accent, -1, 1),
            SlideSeconds = Math.Max(0, x.SlideSeconds),
            PitchOffsetSemitones = x.PitchOffsetSemitones,
            VowelHold = Math.Clamp(x.VowelHold, 0.5f, 2f),
            ConsonantDrive = Math.Clamp(x.ConsonantDrive, 0, 1)
        }).ToArray();

        var o = request.Options;
        var nativeOptions = new NativeMethods.NativeOptions
        {
            PitchCorrectionStrength = Math.Clamp(o.PitchCorrectionStrength, 0, 1),
            FormantPreserve = Math.Clamp(o.FormantPreserve, 0, 1),
            VibratoDepthCents = Math.Clamp(o.VibratoDepthCents, 0, 100),
            VibratoRateHz = Math.Clamp(o.VibratoRateHz, 0, 12),
            OutputGain = Math.Clamp(o.OutputGain, 0.05f, 2f),
            MinPitchHz = Math.Clamp(o.MinPitchHz, 40, 400),
            MaxPitchHz = Math.Clamp(o.MaxPitchHz, Math.Max(80, o.MinPitchHz + 20), 1400),
            BaseMidiNote = o.BaseMidiNote is >= 0 and <= 127 ? o.BaseMidiNote : -1,
            SingingMinMidiNote = o.SingingMinMidiNote is >= 0 and <= 127 ? o.SingingMinMidiNote : -1,
            SingingMaxMidiNote = o.SingingMaxMidiNote is >= 0 and <= 127 ? o.SingingMaxMidiNote : -1
        };

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.OutputSingingWavPath))!);
        var error = new StringBuilder(2048);
        var rc = NativeMethods.resone_render_singing(
            request.InputSpeechWavPath,
            request.SpeechText,
            request.VocalMidiPath,
            nativeGuidance.Length == 0 ? null : nativeGuidance,
            nativeGuidance.Length,
            ref nativeOptions,
            request.OutputSingingWavPath,
            error,
            error.Capacity);

        if (rc != 0) throw new InvalidOperationException($"Vocal synthesis failed ({rc}): {error}");
        return request.OutputSingingWavPath;
    }
}
