# Resone architecture

## Boundaries

`wds.resone.ui` is a C++20 iPlug2 instrument/editor compiled twice: VST3 and APP. Both use the same HTML/CSS/JS editor, voice recorder and audio engine. The WebView carries UI events to the C++ controller; it does not run inference or synthesizers.

`wds.resone.api` owns the music composition service, source-generated JSON contracts, model clients, speech adapters, Resonator bridge, and the C ABI. The native library exports the WebSocket bridge (`resone_open`, `resone_send`, `resone_close`) plus synchronous local MIDI functions (`resone_render_midi`, `resone_export_lane_midi`, `resone_export_project_midi`). C++ loads this library once and leaves it loaded, as NativeAOT library unloading is unsupported. WebSocket callback memory is borrowed for the call; local MIDI functions return owned buffers that C++ copies and releases with `resone_free_buffer`.

`wds.resone.launcher` is a separate NativeAOT process. It owns model/runtime provisioning, the long-lived in-process llama.cpp worker state, and the persistent `/ws` endpoint. In local-inference mode it does **not** start `llama-server`: a tiny Resone bridge dynamically loads the platform-selected downloaded llama.cpp engine pack, warms the GGUF before the stack reports ready, and keeps the model/context resident until the AI stack/worker stops. The VST process never owns GPU inference. Run the launcher with `--no-ui` while using a DAW.

`wds.resone.resonator` contains the Resonator parser, composition types and MIDI writer. The UI reaches this code in-process through `wds.resone.api.dll`; notation rendering, single-lane drag MIDI, whole-project multitrack drag MIDI, and normal Export do not traverse the launcher WebSocket.

## Event protocol

Every command is a UTF-8 JSON message `{op, requestId, payload}`. Connections carry fragmented messages correctly, enforce a 16 MiB limit, and serialize outbound sends through bounded channels. One generation/transcription/export job runs per connection. Cancellation does not require reconnecting.

| Command | Payload | Replies |
|---|---|---|
| `compose` | project, laneId, description, useAhd | status, composition/error/cancelled |
| `transcribe` | wav: base64 WAV | transcript/error |
| `speak` | text | numbered speechChunk, speechEnd/error |
| `render` | notation | midi/error (launcher compatibility path; the UI uses the local C ABI) |
| `export` | SongProject | midi/error (launcher compatibility path; the UI uses the local C ABI) |
| `cancel` | empty | cancelled |

UI project, transport, settings and recording commands stay local. AI/speech/license requests use the Resone WebSocket. MIDI rendering/export/drag from the native UI uses the synchronous local C ABI and the included Resonator code directly. Local LLM inference is llama.cpp in-process inside the launcher worker; the engine directory is selected by platform/RID and its GGML backends are dynamically loaded. The HTTP LLM URL remains only an explicit fallback when local inference is disabled. Qwen/Whisper keep their configured service endpoints. HTTP model downloads retain resume support. `requestAnimationFrame` only paints the playhead; it never queries the server.

## Early audio and threading

1. The composer receives the model stream and validates the completed notation once. Invalid output fails fast; there is no automatic repair-model retry.
2. Resonator returns a note timeline. The native worker applies each lane's actual SoundFont bank/program, mute/solo and gain, and renders blocks of 1,024 stereo frames.
3. Playback starts with roughly 50 ms buffered when initial rendering is fast, or 250 ms otherwise. The producer stays only about 500 ms ahead; it does not render a whole audio file first. This threshold measures initial render time, not LLM token speed.
4. The host callback reads a bounded, generation-tagged single-producer/single-consumer ring. It does no allocation, network, file I/O, synthesis, or mutex acquisition. Underruns output silence, pause the musical position, and resume when the threshold is reached. Old generations are discarded after stop/replacement.
5. Atomic wait/notify wakes the worker from host consumption. Win32 messages dispatch worker/API events to the thread on which the editor opens. No status polling, timer queue draining, or polling in `OnIdle` is used.

FluidSynth runs only on its worker. GeneralUser GS bank/preset enumeration feeds the instrument selector; names are not a hand-maintained GM list. Each lane uses a distinct synthesis channel, with MIDI channel 10 reserved for drums. Whole-project export is Standard MIDI Format 1 with a conductor track plus one independent track per audible lane; lane drag/export uses a single-track MIDI file. Both paths emit bank/program events and preserve clip length.

Voice input captures up to 90 seconds of mono 16 kHz PCM from the default Windows microphone. Send submits one WAV to Whisper, and the returned transcript immediately triggers composition with the selected lane/context. Cancel discards the capture. This is the input flow; Qwen output is separately available through `speak`. Set a registered TTS voice or configure a reference WAV for the Base clone model.

## Reuse and necessary changes

Copied: Resonator.Core, MusicCompositionService, ResonatorNotationValidator, prompt documents, flexible-length rules, one repair pass, default model choices/URLs and Qwen/Whisper request conventions.

Adapted: namespaces, source-generated serialization for NativeAOT, dependency composition, C ABI/WebSocket transport, launcher manifest, selected-lane context and program-change MIDI output. The original WPF/Android views, relationship state, Star voice clones, animation and gameplay services are deliberately outside this new app.

The C++ editor, FluidSynth playback and model supervisor are new implementations of those boundaries, not byte-for-byte copies of the WPF/NAudio classes. WPF and NAudio are not suitable as a NativeAOT VST UI/audio dependency.

## Current extension points

- The VST uses its own Play control and renders audio to the host; DAW transport synchronization, incoming MIDI performance and MIDI-out are not implemented or advertised in its capability flags.
- Local instrument audition starts after notation validation, not while unvalidated LLM tokens arrive.
- TTS is exposed as ordered PCM chunks through the API; this music editor has no chat/TTS audition button.
- The first native editor targets Windows x64. The C ABI and audio core have been built/tested on Linux, but iPlug2/WebView2/WinMM require Windows testing.
- API settings persist per user; standalone work persists in song.json; VST work is stored in the DAW's state chunk. Prompt history and undo are retained within the open editor session.
