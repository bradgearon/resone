using System.Runtime.InteropServices;
using System.Text;

namespace Wds.Resone.Api.VocalSinging;

internal static class NativeMethods
{
    private const string Dll = "wds.resone.vocals";

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeGuidanceEvent
    {
        public double TimeSeconds;
        public double DurationSeconds;
        public float MelodyInfluence;
        public float RhythmInfluence;
        public float Accent;
        public float SlideSeconds;
        public float PitchOffsetSemitones;
        public float VowelHold;
        public float ConsonantDrive;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeOptions
    {
        public float PitchCorrectionStrength;
        public float FormantPreserve;
        public float VibratoDepthCents;
        public float VibratoRateHz;
        public float OutputGain;
        public int MinPitchHz;
        public int MaxPitchHz;
        public int BaseMidiNote;
        public int SingingMinMidiNote;
        public int SingingMaxMidiNote;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeVoicePitchProfile
    {
        public float BasePitchHz;
        public float LowPitchHz;
        public float HighPitchHz;
        public float VoicedFraction;
        public int BaseMidiNote;
        public int BaseOctave;
        public int SingingMinMidiNote;
        public int SingingMaxMidiNote;
    }

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern int resone_analyze_voice_profile(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string inputWavPath,
        int minPitchHz,
        int maxPitchHz,
        out NativeVoicePitchProfile profile,
        StringBuilder errorBuffer,
        int errorBufferLength);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern int resone_render_singing(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string inputWavPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string spokenTextUtf8,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string midiPath,
        [In] NativeGuidanceEvent[]? guidance,
        int guidanceCount,
        ref NativeOptions options,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string outputWavPath,
        StringBuilder errorBuffer,
        int errorBufferLength);
}
