# Resone

A separate music application extracted from Six Stars: NativeAOT C# API/launcher, the supplied Resonator engine, and a shared C++20 iPlug2 VST3/standalone editor. The editor follows the supplied Resone design and uses your pixel logo.

## Build on Windows x64

Install Visual Studio 2022 with **Desktop development with C++** and a Windows SDK, .NET SDK **9.0.203** (or a compatible newer 9.0 feature band), Git, CMake 3.25+, and the Microsoft WebView2 Evergreen Runtime. Run from an x64 Visual Studio developer PowerShell:

```powershell
cd Resone
./scripts/build-windows.ps1
```

The script fetches pinned iPlug2/VST3 SDK sources and builds FluidSynth through pinned vcpkg, then builds:

- `wds.resone.api.dll` — NativeAOT C ABI exposing in-process Resonator MIDI render/export plus the WebSocket bridge used for AI/launcher services.
- `wds.resone.launcher.exe` — NativeAOT host and inference-process owner.
- `wds.resone.ui.exe` — standalone iPlug2 app.
- `Resone.vst3` — the same editor/audio engine as a VST3 bundle.

The default installation folder is `%LOCALAPPDATA%\Wds\Resone`. Override it with `-InstallDir`; set `RESONE_HOME` to that custom folder before starting your DAW so the plugin can find the API DLL, web editor and SoundFont. Default installations require no environment variable.

The GitHub Actions workflow `Windows native builds` runs the same script on Windows when manually dispatched. This source bundle does not contain prebuilt Windows executables.

## Run locally

Start `wds.resone.launcher.exe`. It starts the WebSocket host, acquires missing model files, starts your native inference services and opens the standalone editor. Downloads can be large. Existing model files are reused. Download/checksum/runtime paths are configurable in `config/runtime.json`.

Native engine packs are **not** included in the source bundle. The local LLM stays an in-process shared-library pack, while Whisper and Qwen TTS are private managed server processes, for example:

```text
engines/llm/llama-cpp-dynamic-win-x64/llama.dll
engines/tts/qwenttscpp-nvidia-win-x64/qwen-server.exe
engines/asr/whispercpp-nvidia-win-x64/whisper-server.exe
```

Put each runtime and its matching CUDA/backend DLLs in its engine pack and configure the HTTPS ZIP URL and SHA256 in `config/runtime.json`. llama.cpp is pinned to release `b9870`; qwentts.cpp is pinned to commit `a8a7716b530e49fed537c57711247c12fbbb903c`. Qwen's normal server build statically links the qwentts public API into the executable, so Resone does not require `qwen.dll`.

If your inference services already run, set their URLs in `config/appsettings.json` and start:

```powershell
./wds.resone.launcher.exe --no-services
```

For a DAW, run `wds.resone.launcher.exe --no-ui` and add the built `Resone.vst3` bundle to your DAW's VST3 search path. Use the plugin's Play control; audio goes through the DAW track. The standalone version provides the normal iPlug2 audio device preferences.

## Music workflow

1. Select a lane and its GeneralUser GS instrument. Instrument names/banks are read from the bundled SoundFont.
2. Enter tempo, meter and target bars. Describe the music, or press **Speak your idea**, then **Send voice**. Cancel appears beside recording. Sending a recording transcribes it and immediately submits that text for music generation.
3. The existing music instructions, interval guide and AHD references guide the selected lane. Subsequent requests include its existing notation and other lane context. Manual note edits are supplied as a timeline when revising.
4. The generated notes appear in the piano roll and begin playing after the initial short audio buffer. Playback does not wait for a complete WAV. A valid melody can be shorter or longer than the requested bars; the existing final-measure completion logic is retained.
5. Double-click to add a note, drag to move it, drag its right edge to resize, or right-click to delete. Changes stop the old render. Play auditions the updated song. Use mute, solo and lane volume to mix.
6. Export writes Standard MIDI Format 1 with independent tracks per audible lane. Individual lanes and the full multitrack arrangement can also be dragged to a DAW. New song clears the current work. Standalone work and the API URL are saved under `%LOCALAPPDATA%\Wds\Resone\user`; VST project state is stored with the DAW project.
7. The **Vocals** lane is a pitched MIDI lane. Enter lyrics/text and a Qwen voice, then **Render singing** to generate a local vocal WAV from the vocal MIDI. Explicit `VocalGuidanceEvent` data can override the automatically derived per-note guidance.

Settings (gear) saves a `ws://` or `wss://` host URL and reconnects. The default host listens on loopback `ws://127.0.0.1:8078/ws`. Exposing a remote host requires your own authenticated TLS proxy; this development host does not implement account authentication.

## TTS / vocals configuration

The Vocals lane uses Resone's managed qwentts.cpp server processes. `QwenTtsServiceManager` starts `qwen-server.exe` (falling back to upstream `tts-server.exe`) on a private loopback port only when voice work begins. VoiceDesign and saved/reference-voice generation are separate logical services; only one GPU-resident Qwen model is kept active at a time.

`serviceDelays` in `config/appsettings.json` controls idle shutdown in milliseconds, for example `{ "voice-design": 3000, "custom-voice": 3000 }`. Opening the **New voice** window immediately loads/pins VoiceDesign; closing it releases the pin and starts the idle countdown. Selecting/rendering a saved voice warms the custom-voice service and each request refreshes its deadline. A request is never terminated while it is active.

Saved Resone voices persist their generated/imported WAV plus Whisper transcript. When a saved voice is first used after a custom-voice server start, Resone registers it through `/v1/audio/voices` using `wav_b64` + `ref_text`; the server extracts/caches the clone conditioning in-process. Arbitrary reference cloning is a qwentts **Base** checkpoint feature, so the logical `custom-voice` service intentionally uses `qwenTtsTalkerPath` (the Base model) rather than the named-speaker CustomVoice checkpoint. For singing, Resone asks Qwen to articulate each lyric word once, aligns those words to the vocal MIDI by musical duration, preserves each consonant attack/release once, and stretches the vowel body across as many melody notes as the word covers while pitch follows the MIDI. The source cursor never loops back to re-say a word. Imported-voice transcripts can be corrected before saving so Qwen receives matching reference text.

Whisper spoken-prompt transcription is also local. On Windows x64, pressing **Speak your idea** starts microphone capture immediately and asynchronously warms Resone's private bundled runtime from `engines/asr/whispercpp-nvidia-win-x64/whisper-server.exe` with `models/asr/ggml-large-v3-turbo.bin`. The worker owns that process and its private loopback endpoint; no separately-running Whisper service is required. The same warmed runtime is reused by Voice Designer transcription.

## Verification and limits

Verified here on Linux:

- C# solution compilation.
- NativeAOT publication of both the API shared library and the launcher.
- Actual exported C ABI → WebSocket host → deterministic inference test: notation repair, flexible duration, MIDI conversion/export, transcription, ordered TTS chunks, cancellation and a succeeding request after cancellation.
- Real FluidSynth + bundled GeneralUser GS: 287 enumerated presets, stereo audio, first output by the second simulated audio callback, pause, stop, and immediate replacement without waiting for the old render.
- JavaScript syntax check.

The Windows iPlug2 build, WebView2 appearance, physical microphone and DAW behavior have **not** been executed in this Linux environment. The included Playwright editor test is ready to run on a machine with a working browser; browser launch here was blocked by the environment's socket restrictions. Do the Windows build and DAW smoke test before release.

This version provides the plugin's own transport and audio output. DAW transport synchronization, incoming MIDI performance, and live MIDI-out are not yet implemented; MIDI file drag/export is implemented. The original music generation rules were retained; WPF/NAudio rendering and views were replaced at the native plugin boundary. Details and reuse provenance are in `docs/ARCHITECTURE.md`.

## Tests

```text
cmake -S . -B build/core
cmake --build build/core
ctest --test-dir build/core --output-on-failure
```

The audio test needs a FluidSynth runtime at the solution root (`libfluidsynth-3.dll` on Windows, `libfluidsynth.so.3` on Linux) and its normal shared-library dependencies. The Windows build installs these in the app folder; for core tests, copy them beside the root CMake file or pass the installed app directory directly to `resone_audio_test`.

`tests/integration.py` accepts a dotnet executable (or `-` for a native launcher), launcher path, published API shared-library path and source root. It uses Python aiohttp and ctypes, fake inference servers, and the **real** Resone bridge/host/parser. It does not prove the quality of a live model.

`tests/editor.spec.py` uses Playwright with a mock native bridge to exercise the actual HTML/JS editor. The Windows CI workflow builds the native deliverables. See `docs/THIRD-PARTY.md` and the included licenses before distribution.

### Local LLM context behavior
`contextTokens` configures the llama.cpp context window only. Resone does not reserve or cap output tokens. Local generation streams until EOS/cancellation and uses b9870 context shifting if a response grows to the physical window edge.

### Fast vocal renderer loop

From the repository root:

```powershell
.\build-voice-native.ps1
.\build\voice\resone-voice.exe peppy amazing
```

The command renders with the same `native\vocals` DSP sources used by Resone and automatically plays the result through DirectSound. Use `--no-play` to render silently.
