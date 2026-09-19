# Resone Vocals lane and voice library

The Vocals lane remains a normal pitched MIDI lane for composition/editing, but it also stores lyrics, a selected saved voice, optional `VocalGuidanceEvent` data, and the path/signature of its rendered singing WAV. Lyric generation is intentionally a separate future song-generation request.

## Vocal render path

1. Compose or import the vocal melody as MIDI.
2. Enter lyrics and select a saved voice from the Resone voice library.
3. Resone asks the managed private qwen-server Base service for **one connected source phrase**. The native aligner then finds monotonic word boundaries while preserving natural coarticulation.
4. The native singing engine aligns those one-time word utterances to contiguous spans of the vocal MIDI. A word may cover one note, several notes, or part of a long note, but it is never re-triggered just because the melody contains more notes than lyrics.
5. Consonant attack/release material is preserved once. Extra musical duration is directed primarily into the voiced/vowel nucleus, which is TD-PSOLA stretched while pitch follows every MIDI note underneath the word. The source cursor is monotonic and never wraps back to the start of the word.
6. Playback uses the rendered vocal WAV for that lane instead of its General MIDI instrument.

Qwen's checkpoint named `customvoice` is for its built-in named speakers; qwentts.cpp's reference-WAV + transcript cloning API belongs to the **Base** model. Resone therefore implements user-created custom voices with the Base voice-clone path, which is the API that actually accepts the saved sample/transcription requested by the UI.

## New Voice workflow

The New Voice dialog supports two ways to create a reusable voice:

- **Voice Design**: Resone lazy-loads the separate Qwen 1.7B VoiceDesign checkpoint, passes the user's voice-description instruction plus the editable sample sentence, synthesizes a WAV, then transcribes that WAV.
- **Imported WAV**: choose or drag a WAV into the dialog. Resone normalizes it to mono 24 kHz PCM and transcribes it. The detected reference transcription is editable before Save; the saved `ref_text` should exactly match the recorded WAV, which is important for clear zero-shot cloning.

A sample must produce at least four detected words before it can be saved. Preview audio is temporary. The Play button can replay the generated/imported sample. The description/sample text can be changed and generated again before saving.

When the user presses **OK**, Resone saves the original WAV plus the reviewed transcript. When that voice is first used after a `custom-voice` server start, Resone registers the WAV + `ref_text` with `/v1/audio/voices`; the server extracts/caches the clone conditioning for that process. Cancel discards the temporary preview. VoiceDesign and custom-voice server lifetimes are managed independently by `QwenTtsServiceManager`.

The default VoiceDesign sample text is:

> Thank you for using Resone by We Develop Software, I can't wait to hear what you create.

## Voice storage

User voice data is kept under `%LOCALAPPDATA%/Wds/Resone/user/voices`:

```text
library.json
audio/<voice-id>.wav
refs/<voice-id>.spk
refs/<voice-id>.rvq
previews/<temporary-preview-id>/...
```

`library.json` contains voice metadata and `LastSelectedVoiceId`, so the most recently selected voice is a durable user preference. Temporary previews older than one day are cleaned up automatically.

## Singing preview phrases

The New Voice dialog can optionally render a singing preview of **"We develop software"**. That text has six sung syllables, so Resone contains 20 small six-note Resonator interval studies (Fun, Amazing, Strong, Tender, Heroic, Hopeful, Playful, Dreamy, Triumphant, Mysterious, Yearning, Serene, Bright, Dark, Epic, Warm, Adventurous, Celestial, Determined, Peaceful). They are stored in `VoicePreviewMelodies.cs` and use the project's interval-emotion vocabulary as tiny preview gestures.

## Native version pins

- llama.cpp: release `b9870`, commit `2d973636e292ee6f75fadcf08d29cb33511f509f`.
- qwentts.cpp: commit `a8a7716b530e49fed537c57711247c12fbbb903c`, `QT_ABI_VERSION=4`.

Resone uses the managed `qwen-server.exe`/`tts-server.exe` process path for Qwen TTS. `QwenTtsExpectedVersionPrefix` and `QwenTtsLibraryName` remain only as legacy compatibility properties for older merged source files; the active TTS path does not load a Qwen shared library.

Qwen TTS runs out-of-process through Resone's private managed qwen-server/tts-server process. The server is started lazily, health-checked on a private loopback port, and terminated after the configured idle delay; no global DLL search-path changes or `qwen.dll` binding are required.

## Vocal guidance

```csharp
new VocalGuidanceEvent
{
    TimeSeconds = 2.0,
    DurationSeconds = .8,
    Accent = .35f,
    SlideSeconds = .12f,
    MelodyInfluence = 1f,
    RhythmInfluence = 1f,
    VowelHold = 1.25f,
    ConsonantDrive = .7f
}
```

When no explicit guidance is stored, Resone derives one event per MIDI note from note timing and velocity and uses these values as its default shaping behavior.

## Engine/model configuration

The platform engine pack is selected through `qwenTtsEngineDirectories`; Windows x64 defaults to `engines/tts/qwenttscpp-nvidia-win-x64`. The pack contains `qwen-server.exe` (or upstream `tts-server.exe`) plus its matching GGML/CUDA backend DLLs.

`appsettings.json` separately configures:

- `qwenTtsTalkerPath`: Base checkpoint used by Resone's logical `custom-voice` service for arbitrary saved/reference voice cloning.
- `qwenTtsVoiceDesignTalkerPath`: 1.7B VoiceDesign checkpoint, loaded when New Voice is opened/generated.
- `qwenTtsCodecPath`: shared 12 Hz codec GGUF.
- `qwenTtsServerName`: preferred server executable name (`qwen-server.exe`, with `tts-server.exe` fallback).
- `serviceDelays.voice-design` / `serviceDelays.custom-voice`: idle shutdown delays in milliseconds.

The New Voice dialog pins VoiceDesign while open. Saved voices keep their WAV + transcription; the custom-voice server registers them with `/v1/audio/voices` on first use after each process start, then generates through `/v1/audio/speech`.


### Phrase-continuous singing
Nearby lyric words are grouped into phrases. Consonants may anticipate the next beat so vowels land on pitch; adjacent words do not fade to silence; note gain changes are smoothed; automatic short portamento and delayed vibrato connect sustained notes. Real MIDI gaps still create breaths.
