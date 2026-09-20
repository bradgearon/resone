# Validation

Validated in this environment:

- All `.cjs` source/regression tests pass, including song workspaces, async persistence, composer continuity, Whisper warmup/error handling, voice library UI, and the new managed Qwen server lifecycle test.
- `native-startup-regressions.py`, `song-section-parser-regression.py`, `native-abi-pins.py`, `llama-b9870-loader-regression.py`, and `gemma4-chat-template-regression.py` pass.
- The Qwen regression verifies `qwen-server.exe`/`tts-server.exe` discovery, `/health`, `/v1/audio/speech`, `/v1/audio/voices`, independent `voice-design`/`custom-voice` service delays, process-tree termination, dialog pinning, and pinned server staging.
- The pinned a8a7716 server contract is checked for the `instructions` VoiceDesign request field and server tuning flags.

Not executable in this environment:

- Windows/.NET 9/MSVC/CUDA build and live qwen-server model smoke test (toolchain/runtime binaries are not installed here).
- `tests/integration.py` without a built launcher/API path.
- Playwright editor test because the Chromium test binary is not installed.
## Vocal pitch-sync validation — 2026-09-18

- Native vocals C++ compiled successfully with the revised pitch-synchronous grain tracker.
- Synthetic drifting-F0 input rendered to C4 at ~262.3 Hz and E4 at ~328.8 Hz (targets 261.63/329.63 Hz).
- All available `.cjs` regression tests passed.
- `native-startup-regressions.py`, `song-section-parser-regression.py`, `native-abi-pins.py`, `llama-b9870-loader-regression.py`, `gemma4-chat-template-regression.py`, and `vocal-word-span-regression.py` passed.
- Windows/.NET/CUDA end-to-end execution was not available in this Linux validation environment.



## Qwen settings compatibility

- Verified `ResoneSettings` defines `QwenTtsExpectedVersionPrefix` and `QwenTtsLibraryName` for legacy compile compatibility.
- Verified the active Windows settings migration copies server-manager fields and no longer copies stale `qwenTtsMode`/DLL-loader fields.

## Consonant continuity validation — 2026-09-18

- Native vocals library compiles successfully with g++/C++17 in the validation environment.
- `tests/vocal-word-span-regression.py` verifies consonant-safe internal boundaries, tapered consonant-to-vowel joins, phrase continuity, pitch-synchronous F0 correction, formant handling, and male pitch floor.
- Full available JavaScript regression suite passes.
- Native ABI/startup and Python source regressions pass.
- The supplied 2026-09-18 16:01 recording was inspected as PCM; the reported joins correspond to narrow transient spikes and brief energy collapses, matching the abrupt onset/body handoff fixed here.


## Voice register analysis and octave folding

- Generated and imported voice references are analyzed once for median/base F0, base MIDI note/octave, voiced pitch spread, and a conservative two-octave singing register.
- The default singing register is C through B of the detected base octave plus the octave above it (for example, base octave 2 => C2-B3).
- Vocal melody notes outside the stored register are moved only by whole octaves before pitch correction, preserving pitch class while avoiding extreme voice shifts.
- Existing saved voices without profile metadata are analyzed lazily on first vocal render and the profile is persisted.
- The voice designer reports the detected base note/octave and singing range after generation/import.

## Vocal consonant/register/CLI validation

- Native C++ vocal DLL compiled successfully with C++20 after the speech-piece refactor.
- Synthetic register test: a reference whose median would fall in octave 4 now anchors at MIDI 59 / octave 3 with C3-B4 singing range.
- Synthetic `happy` test retained distinct high-derivative consonant/transient regions at both the initial consonant and the internal plosive between two pitched vowel regions.
- All `tests/*.cjs` regressions passed from repository root.
- `vocal-word-span-regression.py`, `native-startup-regressions.py`, and `native-abi-pins.py` passed.
- The container does not provide the Windows .NET/MSVC runtime, so DirectSound playback and the final Windows CLI executable remain Windows-side smoke tests.

## Phoneme-aware vocals validation
- `tests/vocal-phoneme-planner-regression.py` verifies text-guided vowel nuclei, transient/sustainable consonant classes, mixed consonant-run splitting, smooth pitch glides, phrase dynamics, and both native build targets.
- `tests/vocal-word-span-regression.py` now verifies the phoneme-aware renderer rather than the old voiced/consonant binary model.
- Native standalone CMake build passes with `resone_vocals.cpp` + `resone_vocal_phonetics.cpp`.
- Functional synthetic `happy birthday` render passed with distinct h / pp / b / th / d source regions and continuous output energy through the four-note phrase.

## Vocal smoothing/register validation (2026-09-18)

- Standalone native vocals target builds successfully with the revised phonetic module.
- Synthetic register analysis: 110 Hz male reference remains octave 2 / C2-B3; 294 Hz female reference is recalibrated to octave 3 / C3-B4.
- Synthetic `happy birthday` render retains consonant energy and passes the phoneme-aware functional render test.
- On the same synthetic stress case, large sample-to-sample discontinuities (>0.12 normalized amplitude) dropped from 385 to 189 after the smoothing changes.
- Source-level vocal phoneme, register, word-span, song/workspace, Qwen/Whisper, native ABI/startup, persistence, MIDI, and CLI regressions pass.

## Single-consumption consonant + title validation (2026-09-18)

- Standalone native vocal target compiles successfully with `resone_vocals.cpp`, `resone_vocal_phonetics.cpp`, and the new `resone_vocal_articulation.cpp` module.
- Synthetic `happy birthday` functional render completes successfully with 96/99 20 ms windows active across the four-note test phrase, retaining the articulation improvements after the non-looping consonant change.
- `vocal-phoneme-planner-regression.py` verifies separate noise/voiced sustainable classes, mixed/transient single-consumption handling, the monotonic consonant stretcher, and both native build targets.
- `vocal-word-span-regression.py` and `vocal-register-range-regression.py` pass, preserving pitch-synchronous singing and octave folding.
- All `.cjs` source/regression tests pass after updating the song-mode harness for the title lifecycle.
- `song-title-regression.cjs` verifies immediate provisional titles, robust producer-title replacement, rejection of generic placeholders, and repair of existing Untitled workspace metadata.
- Browser/Windows built-launcher integration remains environment-dependent as documented above.

## Vocal glide/rhotic/aspirate validation
- Native standalone vocal target builds successfully with the shared app sources.
- Planner spot-check: `happy` => aspirate h + two nuclei; `birthday` => rhotic `ir` + final `ay` off-glide; `you` => voiced y-glide + one vowel nucleus; `day` => final off-glide.
- Full available Python/CJS regression suite passes, including word-span, register-range, song/workspace/title, voice-library, Qwen/Whisper, async persistence, and native ABI/startup checks.

## Glide/rhotic + live-launcher validation

- Native vocal renderer compiled successfully with `resone_vocals.cpp`, `resone_vocal_phonetics.cpp`, and `resone_vocal_articulation.cpp`.
- `vocal-glide-rhotic-regression.py` passes and verifies glide carving, non-PSOLA glide rendering, stronger aspirate handling, and text-side y/w/r classification.
- `build-launcher-live-regression.py` passes and verifies automatic build-only behavior while the launcher is running.
- Full non-browser Python and CJS regression suites pass.
- Playwright UI validation was not runnable in this environment because the Chromium binary is not installed.

## Composer Design Pass validation
- `node tests/song-composer-design-pass.cjs` — PASS.
- Full `tests/*.cjs` regression suite — PASS.
- Full non-browser/non-built-launcher `tests/*.py` regression suite — PASS.
- `node --check src/wds.resone.ui/resources/web/app.js` — PASS.
- Playwright UI regression could not run in this environment because the Playwright Chromium binary is not installed.
- .NET compile could not be run in this environment because the .NET SDK is not installed.

## Shared AI runtime provisioning validation (2026-09-18)

- `tests/ai-runtime-provisioning-regression.py` passes and verifies `AI_ROOT`, launcher path persistence, package receipts/versioning, active-engine pointers, NVIDIA/AMD/Intel detection hooks, and centralized dependency pins.
- Full `.cjs` source regression suite passes after updating the local-inference regression for shared AI runtime semantics.
- Non-browser/non-built integration Python regressions pass, including launcher live-build, llama ABI pin, Qwen pin, native startup, vocal planner/register, and song parser tests.
- `tests/integration.py` still requires built .NET/native paths and was not runnable here; Playwright remains browser-environment dependent.
- .NET 9 compilation was not executable in this container because the .NET SDK is not installed. The new receipt serialization uses the existing source-generated JSON context so the launcher remains compatible with reflection-disabled AOT publishing.

## Application updater validation

- `tests/updater-regression.py` verifies local update policy, remote manifest shape, launcher-before-UI update ordering, detached updater runner, hash/extraction/rollback/relaunch code paths, VST installer artifact support, build staging, and the single-daily-log regression.
- All CJS regressions pass after adding `logging` and `updates` to runtime config synchronization.
- All standalone Python regressions pass except environment-dependent `editor.spec.py` (Playwright Chromium unavailable) and `integration.py` (requires explicit dotnet/launcher/native/source command-line inputs).
- This container does not contain the .NET SDK/MSVC Windows toolchain, so the Windows NativeAOT publish itself must be compiled on the Windows development machine.

## 2026-09-19 editor validation

- `node --check src/wds.resone.ui/resources/web/app.js`
- `g++ -std=c++20 -fsyntax-only -Inative/include -Inative/vendor native/src/AudioEngine.cpp`
- `python tests/piano-roll-interaction-regression.py`
- Existing CJS/Python source regressions rerun for this package where supported by the container.

### UI startup / DOM contract repair

Validated after the 2026-09-19 startup repair:

- `node --check src/wds.resone.ui/resources/web/app.js`
- `python tests/ui-dom-contract-regression.py`
- `python tests/piano-roll-interaction-regression.py`
- `node tests/vocal-source-selector.cjs`
- `node tests/song-workspace.cjs`
- `node tests/song-mode.cjs`
- `node tests/song-composer-design-pass.cjs`
- `python tests/updater-regression.py`
- `python tests/ai-runtime-provisioning-regression.py`
- `python tests/build-launcher-live-regression.py`

The container does not provide the Windows MSVC/.NET toolchain, so the final Windows publish/link still needs to run on the Windows checkout.
