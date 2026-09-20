# Resone v9 patch notes

## Vocal rendering and song playback fixes

- `Render singing` now sends only the foreground `renderVocals` request. The render owns Qwen custom-voice service startup itself, removing the redundant background warm-up race.
- Qwen TTS model resolution prefers the configured `AI_ROOT`, then falls back through `AiRuntimeRoot.ResolveAsset(...)` to existing/source-tree runtime assets. Changing `AI_ROOT` no longer makes an already-present VoiceDesign/Base/codec model appear missing during development.
- Switching saved songs stops the currently buffered native AudioEngine request immediately, resets transport position/state, then installs the newly loaded project. The next playback therefore uses the new song's lane instruments/rendered vocal rather than the previous song's audio.
- The newly loaded song reselects its first lane and rerenders the shared Instrument/Voice selector from that lane.

## Installer / production packaging carried forward

- Inno Setup installer defaults the application to Program Files and lets the user change the application path.
- Separate AI/models path page; existing `AI_ROOT` is used as the default when present.
- `Set AI_ROOT` is a selectable installer task.
- VST3 installation is checked by default and targets the system Common Files VST3 directory.
- Setup blocks installation while Resone launcher/UI/updater processes are running and blocks VST replacement while the installed module is loaded.
- Re-running Setup reuses previous app/task choices and lets the user confirm/change AI/model paths.
- Local installer test build contains one explicitly generated local-only license key and does not require the production license server.
- Installer preferences are saved for the updater; updater can elevate a protected-file replacement phase and relaunch Resone from the original non-elevated process.
- FluidSynth runtime payload is staged under `third_party/libfluidsynth/` in installed builds.

## Local test license

`RSN-LOCAL-TEST-A2B8BB5E7059709E99E4D2DE`

Build the local installer test with:

```powershell
.\scripts\build-local-installer-test.ps1 -Version 0.0.1-test
```
