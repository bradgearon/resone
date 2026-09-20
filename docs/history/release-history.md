
## Singing word-span rendering

- Fixed the vocal renderer repeating whole lyric words rapidly when a word covered multiple MIDI notes. The old renderer selected/reused a speech region per note and wrapped its PSOLA source cursor.
- Singing is now **word-span based**: Qwen articulates every lyric word once; Resone assigns each word a proportional contiguous melody span; consonant attack/release material occurs once; vowel material carries the extra duration and follows every MIDI pitch inside the span.
- The TD-PSOLA sustain cursor is monotonic and never wraps to the beginning of the word. This removes the `word word word` machine-gun effect while preserving rhythmic note changes.
- Male/reference voices use a 50 Hz analysis floor and octave-normalize local F0 estimates against the whole voice sample before pitch correction.
- Imported/generated voice transcription is editable before Save and the corrected value becomes Qwen `ref_text`.
# Release update — managed Qwen TTS services

Qwen TTS no longer requires an in-process `qwen.dll`. Resone now owns the qwentts.cpp server lifecycle through `QwenTtsServiceManager` and launches `qwen-server.exe` (with `tts-server.exe` fallback) from the configured engine pack.

- Opening **New voice** starts and pins the VoiceDesign server immediately.
- Closing the window releases it; idle shutdown uses `serviceDelays.voice-design`.
- Selecting or rendering a saved voice warms the logical `custom-voice` service; idle shutdown uses `serviceDelays.custom-voice`.
- Active generation is protected from idle termination, and repeated activity refreshes the deadline.
- Only one Qwen GPU service remains resident at a time; switching modes stops the inactive server before loading the next model.
- Saved voices now persist WAV + Whisper transcript instead of DLL-generated latent files. The managed Base server registers each saved voice through `/v1/audio/voices` (`wav_b64` + `ref_text`) once per process lifetime and then synthesizes through `/v1/audio/speech`.
- VoiceDesign generation also uses `/v1/audio/speech` with the design instruction.
- Generic `SpeechService.SpeakAsync()` was moved off the deleted Qwen DLL runtime as well.
- The Windows Qwen staging script now builds the pinned `tts-server` target at `a8a7716b530e49fed537c57711247c12fbbb903c`, copies it as `qwen-server.exe`, and atomically stages its matching GGML/CUDA DLLs.
- Runtime manifests now require `qwen-server.exe`, not `qwen.dll`.
- VoiceDesign requests use the pinned server's OpenAI-compatible `instructions` field (not the CLI-only/internal `instruct` name).

Default configuration:

```json
"serviceDelays": {
  "voice-design": 3000,
  "custom-voice": 3000
}
```
## 2026-09-18 — pitch-synchronous singing correction

- Reworked vowel pitch processing so source grains follow measured local F0/pitch periods rather than arbitrary positions inside the vowel.
- The renderer now removes spoken F0 drift as part of pitch-synchronous resynthesis and spaces destination grains from the exact MIDI target contour.
- Large source pitch octave errors are still normalized against the voice reference before resynthesis.
- `formantPreserve` now participates in the synthesis path instead of being effectively unused. Upward transpositions receive a small register-aware timbre compensation after pitch placement so low male references do not become excessively boomy at higher melody notes.
- Added a 35 Hz sub-rumble high-pass after overlap/add. This remains below the supported vocal pitch range and is not intended as audible bass EQ.
- Consonant onset/coda handling and the one-word-per-melody-span behavior from the previous singing update are retained.
- Native synthetic validation used a spoken source gliding roughly 105–135 Hz and verified rendered targets at about 262.3 Hz (C4) and 328.8 Hz (E4).



## Legacy Qwen settings compatibility

- Restored `QwenTtsExpectedVersionPrefix` and `QwenTtsLibraryName` on `ResoneSettings` as compatibility-only properties so older merged source files compile.
- The active TTS implementation still uses `QwenTtsServiceManager` + `qwen-server.exe`; these two DLL-era values are not used at runtime.
- Fixed `build-windows.ps1` to propagate the current server settings (`qwenTtsServerName`, `qwenTtsStartupTimeoutSeconds`, and `serviceDelays`) instead of stale DLL-loader fields.


## 2026-09-18 - Phrase-continuous vocal rendering
- Qwen generates one fluent lyric phrase instead of isolated per-word speech.
- Internal words no longer fade to silence.
- Consonants can anticipate the beat and overlap the preceding vowel.
- Added short automatic legato portamento and delayed sustain-dependent vibrato.
- Smoothed note-to-note dynamics while preserving real rests/breaths.

## 2026-09-18 — Consonant continuity / de-click pass

Analyzed the supplied singing-preview recording and found narrow transient spikes plus short energy collapses at several internal word joins. The phrase-continuity renderer was still independently trimming word edges and the anticipated onset ended abruptly where the pitch-synchronous vowel began.

Changes:
- internal fluent-phrase word regions now preserve their exact shared boundaries instead of running per-word energy trimming;
- low-energy consonants such as s/f/th and plosive releases are therefore not discarded as silence;
- anticipated consonants get both attack and release tapers;
- the pitched vowel fades in under the consonant tail over the same join window;
- the prior sustained vowel is not ducked while the next consonant approaches, preventing audible level holes;
- maximum anticipated consonant duration is slightly reduced so consonant clusters do not appear to jump far ahead of the beat;
- coda limits were tightened slightly while preserving phrase-final releases.


## Voice register analysis and octave folding

- Generated and imported voice references are analyzed once for median/base F0, base MIDI note/octave, voiced pitch spread, and a conservative two-octave singing register.
- The default singing register is C through B of the detected base octave plus the octave above it (for example, base octave 2 => C2-B3).
- Vocal melody notes outside the stored register are moved only by whole octaves before pitch correction, preserving pitch class while avoiding extreme voice shifts.
- Existing saved voices without profile metadata are analyzed lazily on first vocal render and the profile is persisted.
- The voice designer reports the detected base note/octave and singing range after generation/import.

## 2026-09-18 — consonant-preserving vocal regions, practical register, DirectSound CLI

- Replaced the single-vowel-nucleus word renderer with ordered voiced/consonant speech pieces.
- Consonants such as h/p/b/d/t/k/s/f are preserved once at near-natural timing and are never pitch-shifted; voiced regions absorb sustained musical duration.
- Added 2.5 ms internal equal-power joins and removed the old anticipated-onset path that could cause jumps/skips.
- Tightened fluent-phrase word-boundary search so internal plosive closures are less likely to be mistaken for word boundaries.
- Voice register analysis now anchors on the lower stable voiced quintile instead of median speech F0. Pitch profile v2 forces older saved profiles to be reanalyzed.
- `resone-voice` now plays the rendered WAV automatically via NAudio `DirectSoundOut`; pass `--no-play` for silent runs.

## Phoneme-aware singing pass
- Added `native/vocals/resone_vocal_phonetics.*` so lyric text is planned as vowel nuclei plus consonant behavior runs instead of treating an entire word as one pitch-bearing unit.
- Text vowel nuclei now guide acoustic voiced-island splitting/merging. Multi-syllable words such as `happy` and `birthday` retain multiple vowel nuclei even when the pitch detector stays voiced through an internal sonorant.
- Consonants are split into two musical classes:
  - **Transient:** stops/affricates such as p/b/t/d/k/g/ch stay near their natural closure/release duration and are never used to fill a long note.
  - **Sustainable:** fricatives, breath, nasals, and liquids such as s/f/sh/th/h/m/n/l/r/v/z may take a controlled share of extra musical duration. Voiced sustainables may follow the note pitch; noisy sustainables are granular-stretched without spectral down-pitching.
- Mixed clusters are split by behavior. For example, `st` can sustain the `s` while preserving the `t` transient.
- Pitch slides now interpolate smoothly in log-frequency/cents space instead of linearly in Hz.
- Added a mild phrase-level dynamics contour so connected words read more like one performed line.
- Both the main UI target and the isolated `resone-voice` native target compile the same new phonetics module.

## 2026-09-18 vocal smoothing + register re-profile

- Bumped saved voice pitch profiles to version 3. Version-2 profiles are automatically reanalyzed.
- Selecting an old saved voice now reanalyzes its register immediately.
- Added `resone-voice ... --reprofile` to force register analysis from the CLI.
- Practical register analysis now moves speaking anchors at C4 or above down one octave before building the two-octave singing window (for example D4 speech -> D3 practical anchor, C3-B4 window).
- Final silent `e` no longer invents an extra vowel nucleus in the text-guided singing planner.
- Mixed consonant clusters are preserved as one natural acoustic gesture unless real phoneme timestamps are available.
- Replaced equal-power adjacent-phoneme joins with raised-cosine constant-sum crossfades to remove short gain pulses.
- Word-boundary de-clicking now applies a decaying offset correction instead of extrapolating the previous waveform into the new consonant.
- Sustainable/vowel joins use a smoother 4 ms overlap while hard transient consonants keep a shorter 1.5 ms join.

## 2026-09-18 — single-consumption sung consonants + song-title repair

### Vocal articulation

- Split consonant rendering into a dedicated `resone_vocal_articulation.cpp/.h` module so phonetic planning, articulation/time-stretch, and pitch-synchronous vowel rendering are no longer mixed together in one large implementation.
- Removed the modulo/grain-loop stretcher that could audibly repeat sustainable consonants such as `s`, `f`, `sh`, `th`, or `h`.
- Unvoiced sustainable consonants now use a monotonic overlap-add time stretch. Their attack and release are consumed once; only the continuous interior noise is expanded.
- Voiced sustainable consonants (`m`, `n`, `l`, `r`, `v`, `z`, `ng`, `zh`) have their own musical path and follow the MIDI pitch contour with restrained vibrato when the source contains a usable F0.
- Hard stops/affricates and mixed consonant clusters are single-consumption events and are never used as a looping sustain reservoir.
- Sustainable consonant expansion is capped when a word contains vowels; excess musical duration is reassigned to the longest vowel nucleus so long notes remain vowel-led like normal singing.
- Text phonetic planning now distinguishes voiced sustain, noise sustain, transient, and mixed consonant behavior rather than one generic `Sustainable` class.

### Song titles

- Song workspaces receive a provisional non-generic title from the song request as soon as Song mode starts, preventing autosave from filling the library with `Untitled Song` while the producer is still running.
- The producer title replaces that provisional title when the SongDesign response arrives.
- Producer-title parsing now tolerates bullets/bold Markdown around `Song title:` and rejects placeholder values such as `Untitled Song`, `Untitled`, `New Song`, or `Song`.
- Existing saved `Untitled Song` workspaces are repaired on library load when producer notes/history are available. Resone recovers the producer title or falls back to the original generation brief and persists the repaired metadata/index.

## Vocal articulation refinement
- Initial `y`/`w`/`r` are now voiced glides rather than transient consonants, preventing words such as `you` from disappearing at the onset.
- Post-vocalic `r` is folded into the preceding rhotic vowel nucleus, so words such as `birthday` do not paste a normally-spoken `r` between sung vowel regions.
- `-ay`/related `y` off-glides are reserved near the end of the vowel nucleus instead of being stretched across the whole note.
- `/h/` has a dedicated aspirate renderer with controlled breath brightness/presence.
- Unvoiced sustainable consonants use a bounded one-pass monotonic warp rather than repeated overlap-add grains.
- Speech-word alignment preserves low-energy material between activity islands and enforces a minimum acoustic source region per lyric word.

## 2026-09-18 – Glide/rhotic cleanup and live-launcher builds

- Initial `y/w/r` glides are now explicitly carved from the front of a voiced source island when the pitch detector would otherwise absorb them into the vowel.
- Glides are rendered once as natural formant transitions instead of being PSOLA/autotuned as independent mini-notes. This removes the growly/gargled onset-r artifact and makes `you` retain an audible `y -> oo` transition.
- Mixed onsets such as `br` preserve the stop/noise portion separately and reserve only the short trailing voiced transition as the glide.
- `/h/` received a modest additional breath-presence lift while remaining a one-pass aspirate.
- A 2.5 ms fade is applied only to the first audible samples of the final vocal output to suppress the startup glip without softening later consonants.
- `scripts/build-windows.ps1` now detects a running `wds.resone.launcher`. Normal development builds continue into `build/` without touching the live installation, so the launcher can stay running. Close the launcher and run the build again to deploy; `-ForceDeploy` explicitly overrides this protection.

## Song Composer Design Pass
- Added a saved, default-on **Composer design pass** checkbox beside Song mode.
- Song generation can now run a non-track-scoped composer pass after the producer outline and before section/lane generation.
- The pass uses the normal Resone composer instructions/references to create a shared emotional note palette, key plan, Resonator melody seed, motifs, chord progression, and 1-3 Answer/Contrast/Continuation variants total across all three response types.
- AHD is explicitly available for purposeful out-of-key material when enabled; melody material defaults around octave 3, normally occupies octaves 3-4, may peak in octave 5, and never writes octave 6+ in the design packet.
- The resulting design packet is stored in `SongGenerationState` and injected into every later track composer request.

## 2026-09-18 — shared AI_ROOT launcher provisioning

- Moved engine/model provisioning responsibility into the launcher around a shared `AI_ROOT` tree so Resone and Six Stars can share one copy of large AI assets.
- Added launcher `--ai-root <path>` persistence plus `AI_ROOT` / legacy `WDS_AI_ROOT` environment resolution. Default is `%LOCALAPPDATA%/Wds/AI`.
- Added hardware-family selection (`nvidia`, `amd`, `intel`, `apple`, `cpu`, `dynamic`) with Windows display-adapter vendor detection and `AI_PLATFORM` override.
- Extended `config/runtime.json` into a versioned package manifest: engine/model `id`, `version`, HTTPS URL, SHA-256, component/RID/platform, required files, ZIP strip depth, and centralized C++ dependency pins.
- Engine installs are downloaded to `AI_ROOT/work`, SHA-256 verified, safely extracted, required-file checked, and atomically swapped. Each component writes `engines/<component>/.active` so appsettings does not need vendor-specific runtime paths.
- Model downloads are resumable and managed by ID/version/URL/hash receipts. Any managed version/URL/hash change intentionally redownloads the model before replacement.
- Runtime resolution prefers launcher-managed `AI_ROOT` assets but falls back to existing source/install-tree assets during migration/development.
- `build-qwentts-nvidia-win-x64.ps1`, `build-inference-pack.ps1`, and Windows dependency checkouts now read pinned git refs from `config/runtime.json` instead of duplicating commit constants in build scripts.
- Added `scripts/package-ai-engine.ps1`, `scripts/hash-ai-model.ps1`, and `docs/operations/ai-runtime-provisioning.md` for publishing/versioning packages.

## Launcher-owned application updater

- Added `wds.resone.updater.exe`, published beside the launcher.
- Launcher checks a JSON-configured HTTPS release manifest before opening the UI/AI stack.
- Updater runs from an external temporary runner so it can replace the installed launcher/updater safely.
- Update archives are SHA-256 verified, safely extracted, critical extracted files are hashed again, and the staged payload version must match the remote manifest.
- Downloads retry on transport/hash failure; exhausted retries become a recorded update failure rather than silently installing bad bytes.
- Archive installs preserve configured paths, swap through a backup, and roll back if installation or new-launcher startup fails.
- Failed updates relaunch the old/current launcher with a one-shot skip flag; later launches retry after configured backoff. Repeated failures produce tray warnings.
- Remote artifacts also support `installMode: installer`, ready for the future VST3 installer path.
- `scripts/package-resone-update.ps1` creates a version-stamped ZIP, archive SHA-256, critical file hashes, and remote manifest.
- `config/update-manifest.example.json` documents the resone.io release format.

## Unified logging

- Replaced JSONL host trace, per-call LLM JSON files, startup-per-process logs, and `whisper-cpp.log` with one cross-process daily text log.
- Default path: `%LOCALAPPDATA%\Wds\Logs\Resone\resone-YYYY-MM-DD.log`.
- Runtime JSON controls log directory and retention.

## 2026-09-19 — voice-source layout + interactive piano roll

- The Vocals lane now uses the existing header sound selector as **Voice** (`Oohs`, saved voices, `＋ New voice…`); the vocal editor is a full-width lyrics field plus Render button, avoiding the clipped voice controls.
- Timeline/grid clicks move the playback cursor. Playing or paused transport seeks through a native seek-aware render path; stopped transport remembers the cursor for the next Play.
- Volume, mute, and solo are live mixer operations and no longer stop/restart transport. FluidSynth lanes use channel-volume CC and rendered vocal WAVs use the same atomic per-lane gain state.
- Piano-roll lanes use semitone rows and persist Resonator `key=` information as lane/key-region metadata. In-key rows receive the lane accent tint, out-of-key rows are darker, tonic rows are emphasized, and the selected lane shows the current key/tonic for the cursor region.
- Ctrl+wheel zooms horizontally; Alt+wheel zooms vertically. Horizontal zoom, vertical zoom, and per-lane heights persist in local preferences.
- Drag the bottom edge of the left lane strip to resize an individual lane.
- Notes are selectable. Delete/Backspace removes the selected note. Double-click adds a one-quarter-note note snapped to the nearest quarter-note position; Alt+double-click bypasses the time snap. Dragging moves notes and dragging the right edge changes duration; Alt bypasses time snapping while dragging.
- At sufficient vertical zoom, note blocks show pitch labels such as `C#4`.

## UI startup repair (2026-09-19)

- Fixed the piano-roll scroll container DOM contract: `app.js` expected `#rollContent`, but the HTML only exposed `.rollContent`. The resulting null `addEventListener` exception stopped script execution before `render()` and `send('ready')`, leaving the app empty and all controls inert.
- Removed the stale `#newVoice` lookup left over from moving voice creation into the shared Instrument/Voice selector.
- Added `tests/ui-dom-contract-regression.py`, which fails if any `$('<id>')` reference in `app.js` does not exist in `index.html`.
- Clamped rendered lane height to the same minimum used by lane headers so saved/Alt vertical zoom cannot make piano-roll geometry disagree with the left lane controls.
- Added early browser `error` / `unhandledrejection` reporting. UI bootstrap failures now update the status line and send `uiDiagnostic` directly through the native bridge even when the normal ready handshake never completes.
- Added native UI diagnostics to the same daily rolling Resone text log (`%LOCALAPPDATA%\Wds\Logs\Resone\resone-YYYY-MM-DD.log`, or `RESONE_LOG_DIR`). It uses the same named cross-process mutex as the managed logger.
