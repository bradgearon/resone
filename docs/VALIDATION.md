# Validation record — 2026-09-16

Executed in the Linux workspace:

| Check | Result |
|---|---|
| .NET solution compilation, SDK 9.0.203 | Passed |
| NativeAOT API shared library publication, linux-x64 | Passed; all four C ABI symbols exported |
| NativeAOT ASP.NET launcher publication, linux-x64 | Passed |
| ctypes calls into actual native API, connected to actual native launcher | Passed |
| Fake upstream SSE split across content deltas, invalid first score then repair | Passed |
| Requested eight bars; accept valid shorter score and derive duration | Passed |
| Optional bar separators, Resonator MIDI header/track creation | Passed |
| Project MIDI export | Passed |
| WAV submission and Whisper transcript | Passed |
| Optional Qwen voice registration with reference WAV | Passed |
| Irregular upstream PCM byte fragments -> complete numbered chunks | Passed; 24,014 bytes preserved in three chunks |
| Cancel model request, then render another score on the same connection | Passed |
| CMake C++20 audio-core build + CTest | Passed |
| Actual GeneralUser GS enumeration through FluidSynth 2.2.5 | 287 presets |
| Actual stereo synthesis, pause, stop, immediate replacement | Passed |
| Early playback in simulated 48 kHz host callbacks | First audio by callback 2 in this run; not a Windows latency guarantee |
| Node syntax check of editor JavaScript | Passed |

Not executed here: MSVC/iPlug2 APP and VST3 binaries, WebView2 rendering, physical microphone capture, live GGUF inference, DAW restore/transport/device tests. The browser test could not launch Chromium because this environment rejects its Unix socket creation. The included Windows workflow and Playwright editor test have not been represented as passing.

Tests use deterministic upstream responses to isolate the real NativeAOT bridge, host, cancellation, parser and serializer behavior. They do not assess model composition quality. C++ audio tests use the actual included SoundFont and a real FluidSynth shared library.
