# Resone

A separate music application extracted from Six Stars: NativeAOT C# API/launcher, the supplied Resonator engine, and a shared C++20 iPlug2 VST3/standalone editor. The editor follows the supplied Resone design and uses your pixel logo.

## Build on Windows x64

Install Visual Studio 2022 with **Desktop development with C++** and a Windows SDK, .NET SDK **9.0.203** (or a compatible newer 9.0 feature band), Git, CMake 3.25+, and the Microsoft WebView2 Evergreen Runtime. Run from an x64 Visual Studio developer PowerShell:

```powershell
cd Resone
./scripts/build-windows.ps1 -SixStarsRuntimeRoot 'U:\six-stars-solution-2\YOUR-PUBLISHED-APP'
```

`-SixStarsRuntimeRoot` is optional. It copies the `engines` and `models` directories from your existing published app. Use the folder that actually contains those directories, not the source solution folder. The script fetches pinned iPlug2/VST3 SDK sources and builds FluidSynth through pinned vcpkg, then builds:

- `wds.resone.api.dll` — NativeAOT C ABI / WebSocket bridge.
- `wds.resone.launcher.exe` — NativeAOT host and inference-process owner.
- `wds.resone.ui.exe` — standalone iPlug2 app.
- `Resone.vst3` — the same editor/audio engine as a VST3 bundle.

The default installation folder is `%LOCALAPPDATA%\Wds\Resone`. Override it with `-InstallDir`; set `RESONE_HOME` to that custom folder before starting your DAW so the plugin can find the API DLL, web editor and SoundFont. Default installations require no environment variable.

The GitHub Actions workflow `Windows native builds` runs the same script on Windows when manually dispatched. This source bundle does not contain prebuilt Windows executables.

## Run locally

Start `wds.resone.launcher.exe`. It starts the WebSocket host, acquires missing model files, starts your native inference services and opens the standalone editor. Downloads can be large. Existing model files are reused. Download/checksum/runtime paths are configurable in `config/runtime.json`.

Native engine packs are **not** included in the source bundle. The default manifest uses the same layout as Six Stars:

```text
engines/llm/llama-cpp-dynamic-win-x64/llama-server.exe
engines/asr/whispercpp-nvidia-win-x64/whisper-server.exe
engines/tts/qwenttscpp-nvidia-win-x64/tts-server.exe
```

Keep each executable's supporting runtime/CUDA DLLs beside it. The supplied defaults retain the project's NVIDIA/GPU configuration. Adjust engine arguments for your installed builds. The launcher prints download and process output; wait for the inference engine's ready message before generating. “Host connected” means the Resone WebSocket host is reachable, not that a model has finished loading.

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
6. Export writes a combined General MIDI file containing bank/program changes. New song clears the current work. Standalone work and the API URL are saved under `%LOCALAPPDATA%\Wds\Resone\user`; VST project state is stored with the DAW project.

Settings (gear) saves a `ws://` or `wss://` host URL and reconnects. The default host listens on loopback `ws://127.0.0.1:8078/ws`. Exposing a remote host requires your own authenticated TLS proxy; this development host does not implement account authentication.

## TTS / speech configuration

The WebSocket `speak` command retains Qwen's 24 kHz mono signed-16-bit PCM streaming format. TTS models/services are optional and disabled in the default runtime manifest because music generation does not need them. Set `ttsVoice` to a voice registered with your server. For Qwen Base voice cloning, set `ttsReferenceAudioPath` and `ttsReferenceText` in appsettings; the adapter registers that WAV at `/v1/audio/voices` before synthesis. Supply a WAV you have permission to use. Star identities/clone files are not copied into this independent app.

Whisper is included because the requested voice input needs transcription, in addition to the requested LLM/TTS layers.

## Verification and limits

Verified here on Linux:

- C# solution compilation.
- NativeAOT publication of both the API shared library and the launcher.
- Actual exported C ABI → WebSocket host → deterministic inference test: notation repair, flexible duration, MIDI conversion/export, transcription, ordered TTS chunks, cancellation and a succeeding request after cancellation.
- Real FluidSynth + bundled GeneralUser GS: 287 enumerated presets, stereo audio, first output by the second simulated audio callback, pause, stop, and immediate replacement without waiting for the old render.
- JavaScript syntax check.

The Windows iPlug2 build, WebView2 appearance, physical microphone and DAW behavior have **not** been executed in this Linux environment. The included Playwright editor test is ready to run on a machine with a working browser; browser launch here was blocked by the environment's socket restrictions. Do the Windows build and DAW smoke test before release.

This first version provides the plugin's own transport and audio output. DAW transport synchronization, incoming MIDI performance, MIDI-out and drag-to-DAW export are not implemented. The original music generation rules were retained; WPF/NAudio rendering and views were replaced at the native plugin boundary. Details and reuse provenance are in `docs/ARCHITECTURE.md`.

## Tests

```text
cmake -S . -B build/core
cmake --build build/core
ctest --test-dir build/core --output-on-failure
```

The audio test needs a FluidSynth runtime at the solution root (`libfluidsynth-3.dll` on Windows, `libfluidsynth.so.3` on Linux) and its normal shared-library dependencies. The Windows build installs these in the app folder; for core tests, copy them beside the root CMake file or pass the installed app directory directly to `resone_audio_test`.

`tests/integration.py` accepts a dotnet executable (or `-` for a native launcher), launcher path, published API shared-library path and source root. It uses Python aiohttp and ctypes, fake inference servers, and the **real** Resone bridge/host/parser. It does not prove the quality of a live model.

`tests/editor.spec.py` uses Playwright with a mock native bridge to exercise the actual HTML/JS editor. The Windows CI workflow builds the native deliverables. See `docs/THIRD-PARTY.md` and the included licenses before distribution.
