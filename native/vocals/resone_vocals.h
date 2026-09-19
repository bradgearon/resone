#pragma once
#include <cstddef>

#ifdef _WIN32
  #ifdef RESONE_VOCALS_EXPORTS
    #define RESONE_VOCALS_API __declspec(dllexport)
  #else
    #define RESONE_VOCALS_API __declspec(dllimport)
  #endif
#else
  #define RESONE_VOCALS_API
#endif

extern "C" {

struct ResoneVocalGuidanceEvent {
    double timeSeconds;
    double durationSeconds;      // <= 0 means use MIDI note duration
    float melodyInfluence;       // 0..1, pitch-following strength
    float rhythmInfluence;       // 0..1, duration-following strength
    float accent;                // -1..1, amplitude emphasis
    float slideSeconds;          // portamento into this event
    float pitchOffsetSemitones;  // optional additional pitch offset
    float vowelHold;             // 0.5..2, favors vowel sustain vs consonants
    float consonantDrive;        // 0..1, how much attack/release is preserved
};

struct ResoneVocalRenderOptions {
    float pitchCorrectionStrength; // 0..1
    float formantPreserve;          // 0..1, currently approximate via TD-PSOLA
    float vibratoDepthCents;        // 0..100 typical
    float vibratoRateHz;            // e.g. 5.0
    float outputGain;               // linear gain, default 0.95
    int minPitchHz;                 // voice analysis floor
    int maxPitchHz;                 // voice analysis ceiling
    int baseMidiNote;               // analyzed reference center, -1 = unknown
    int singingMinMidiNote;         // octave-fold target floor, -1 = unrestricted
    int singingMaxMidiNote;         // octave-fold target ceiling, -1 = unrestricted
};

struct ResoneVoicePitchProfile {
    float basePitchHz;
    float lowPitchHz;
    float highPitchHz;
    float voicedFraction;
    int baseMidiNote;
    int baseOctave;
    int singingMinMidiNote;
    int singingMaxMidiNote;
};

// Analyze a reference voice once when it is generated/imported/recorded.
// The singing range is intentionally conservative: the octave containing the
// lower stable voiced register plus the octave above it.
RESONE_VOCALS_API int resone_analyze_voice_profile(
    const char* inputWavPath,
    int minPitchHz,
    int maxPitchHz,
    ResoneVoicePitchProfile* profile,
    char* errorBuffer,
    int errorBufferLength);

// Returns 0 on success. Nonzero return writes a readable UTF-8 error into errorBuffer.
RESONE_VOCALS_API int resone_render_singing(
    const char* inputWavPath,
    const char* spokenTextUtf8,
    const char* midiPath,
    const ResoneVocalGuidanceEvent* guidance,
    int guidanceCount,
    const ResoneVocalRenderOptions* options,
    const char* outputWavPath,
    char* errorBuffer,
    int errorBufferLength);

}
