RESONE: layout, publish content, and shared AI stack fix

Close Resone, extract this archive into U:\Resone and overwrite the included files.
If you edited the source config/appsettings.json or config/runtime.json, back them
up first and merge your custom values after extraction.
Run scripts\build-windows.ps1 again with your original arguments/InstallDir.
Run the installed Resone launcher, not an older copy. Six Stars is not part of the Resonator/MIDI path.

Changes:
- Corrects double DPI scaling of the WebView for the standalone app and VST3.
- Keeps Submit in its own space beside the shrinking textarea.
- Copies assets and config on dotnet build/publish, including music instructions.
- Stages assets explicitly and checks music-composition.json is installed.
- STT uses port 8000 and TTS uses 8101 by default. LLM generation defaults to in-process llama.cpp: Resone selects the downloaded engine directory for the current platform (for Windows x64, engines/llm/llama-cpp-dynamic-win-x64), warms the GGUF when the AI stack starts, keeps it resident while the stack is active, streams tokens, and keeps reasoning disabled. Port 8080 is only the explicit HTTP fallback when local inference is disabled.
- runtime.json can reuse already-installed model/runtime folders or let Resone manage its own configured runtimes. Local LLM mode dynamically loads llama.dll/GGML backends from the platform-selected engine directory without starting llama-server. This is separate from the in-process Resonator/MIDI engine.
- The build script migrates the old installed STT URL only if it is still the
  original port-8100 default. Other custom service URLs are preserved.
- Resone's application WebSocket stays ws://127.0.0.1:8078/ws. This is separate
  from the shared AI services; don't point that UI setting at Whisper/LLM ports.

Verification: MSBuild content evaluation confirms the music instruction output
path and build/publish copy metadata. Reviewed native sizing against pinned
 iPlug2 APP/VST3 and WebView source. Full build and Windows UI execution could
not be verified here; local .NET build stopped without diagnostics.

2026-09-16 follow-up fixes:
- Tray status updates now change only the tray tooltip; Resone never requests Windows balloon/toast tray notifications.
- The WebView resolves the staged `web/index.html` from the install root, module ancestors, or canonical LocalAppData install and passes an absolute UTF-8 path to iPlug2. A clear local fallback page is shown if staging is incomplete instead of WebView's "page cannot be displayed" page.
- Lane MIDI drag starts on pointer-down, releases WebView mouse capture, and exposes the staged `.mid` explicitly as `CF_HDROP` through OLE so DAWs and Explorer receive an actual file drag cursor.

VST3 ICON FIX
-------------
The Windows resource script already pointed IDI_ICON1 at resources/Resone.ico, but the pinned iPlug2 CMake path only compiled main.rc into the standalone APP target. Resone-vst3 therefore did not contain the application icon resource.

Fixes in this revision:
- src/wds.resone.ui/CMakeLists.txt explicitly adds resources/main.rc to Resone-vst3.
- scripts/build-windows.ps1 verifies RT_GROUP_ICON resource 40003 exists in both Resone.exe and the actual Resone.vst3 module; the build fails if either is missing.
- The build also stages Plugin.ico + desktop.ini on the .vst3 bundle so Windows Explorer can show the Resone logo for the VST3 bundle instead of a generic/default plug-in folder icon.

Note: A VST editor opened inside a DAW is normally a child window owned by the DAW. Some hosts therefore keep their own taskbar icon even when the plug-in DLL contains the correct icon. The plug-in binary and bundle are now correctly branded; a host-owned taskbar button may still be host-controlled.


VST3 ICON VERIFIER HOTFIX
==========================
The first VST3 icon fix accidentally declared the P/Invoke methods on the
PowerShell Add-Type helper as internal. PowerShell therefore loaded the helper
type but could not call LoadLibraryExW/FindResourceW/FreeLibrary.

This revision makes those methods public and uses NativeResourceCheckV2 so a
broken NativeResourceCheck type left loaded in an existing PowerShell process
cannot poison a rebuild after updating the source. The actual VST3 resource
attachment in CMake is unchanged.

WINDOW/TASKBAR ICON FIX
-----------------------
The embedded Resone.ico resource is now explicitly loaded from the module that
contains Resone code and assigned to the native editor HWND with WM_SETICON for
both ICON_SMALL and ICON_BIG. This fixes the generic caption and taskbar icons.
The standalone also applies the icon to its top-level/root HWND. VST3 resource
loading deliberately uses GetModuleHandleEx(FROM_ADDRESS), because
GetModuleHandle(nullptr) inside a VST3 points at the DAW executable rather than
the Resone plug-in module.

Windows icon compile follow-up (2026-09-16)
- Fixed a Windows SDK macro collision in native/include/WindowsSupport.hpp.
- The Windows headers define the legacy token `small` as a macro, so local HICON variables named `small` caused MSVC to parse `HICON char` and emit C2628/C2062 plus misleading SendMessageW/SetClassLongPtrW errors.
- Renamed the local handles to `smallIcon` and `largeIcon`. No icon behavior was removed; the title-bar/taskbar WM_SETICON logic remains in place.
2026-09-16 repeat-build VST3 deployment fix:
- Removed the desktop.ini/Plugin.ico shell-decoration hack from Resone.vst3. It was unrelated to the embedded/native window icon and caused AccessDenied on later Copy-Item deployments because the bundle/file attributes were ReadOnly/Hidden/System.
- build-windows.ps1 now clears those legacy attributes and deletes old desktop.ini/Plugin.ico files from the build output, LocalAppData install, and iPlug2 auto-deploy VST3 location before building/copying. Existing affected installs repair themselves on the next build.


2026-09-16 silent musical narrative planning:
- Every generation now performs a silent streaming MusicNarrative LLM call before the notation/composition call.
- The narrative planner uses interval_emotion_field_guide.md and returns plain-text ordered feelings with interval + perspective, phrasing, and continuation guidance.
- Feelings that need more than one interval/perspective are explicitly instructed to list all required gestures and how they combine or sequence.
- AHD-enabled requests tell the narrative planner not to reject divergent/chromatic expressive colors as out of tune.
- The normal music request still contains the user's original description unchanged; the generated narrative is appended afterward under SUGGESTED MUSICAL NARRATIVE and is explicitly subordinate to the user's request.
- Both ArrangementComposer (the current UI path) and MusicCompositionService use the same MusicNarrativePlanner.

2026-09-16 narrative-plan enforcement update:
- The silent MusicNarrative pass now receives tempo, meter, and bar count and outputs phrase/bar placement rather than a floating emotional list.
- Each phrase plan includes feeling, emotional transition, required interval/perspective gestures, phrasing role, continuation target, placement/emphasis, and payoff/memory behavior.
- AHD planning explicitly considers tonal-home, activated-material, and narrative-history perspectives so a chromatic note can retain a learned emotional meaning instead of being treated as wrong.
- The second music-generation request now labels this as MUSICAL NARRATIVE PLAN — FOLLOW THIS PLAN and requires every listed interval gesture to occur recognizably in the actual notes. Register changes or generic mood cannot substitute for required intervals.
- Music composition and repair instructions preserve the plan's emotional order, approximate placement, continuation behavior, and payoff design.

2026-09-16 narrative-driven arrangement generator:
- The silent MusicNarrative pass now owns emotional-story and interval/perspective planning.
- ArrangementComposer is explicitly the music realization stage: it must follow the narrative plan instead of inventing a second emotional arc.
- The active arrangement prompt no longer includes the full interval-emotion guide; that guide is consumed by the planner. The composer keeps AHD and Resonator references plus a concise execution guide.
- Required narrative interval gestures remain hard compositional requirements, while generator instructions now focus on selected-lane realization, timing, context coordination, syntax, and repair.

2026-09-16 parser/generation resilience:
- Removed the LLM repair retry from both arrangement and legacy music-composition generation. Invalid output now fails immediately instead of making a second slow model call.
- Resonator dynamics remain canonical bare tokens (pp p mp mf f ff); the prompt explicitly forbids p=mf-style output, while the parser defensively accepts p=/dyn=/dynamic=/dynamics= aliases.
- Rest and hold event separators no longer require whitespace when unambiguous: C5_ == C5 _, and C5- == C5 -. Negative octaves, cents offsets, and onset offsets are preserved.

2026-09-17 full-song foundation + desktop/MIDI workflow:
- Added dormant full-song infrastructure without changing the existing Compose UI path.
- New SongDesign model pass streams a plain-text long-form song outline (sections, emotional arc, motif strategy, harmonic/AHD strategy, instrumentation and transitions).
- Added SongGenerationState + SongGenerationProvisioner. The provisioner deterministically decides what each chunk receives: global design, song-so-far summary, current section plan, recent section summaries, and a bounded collection of exact important Resonator motif/chord/bass/rhythm fragments.
- Added songChunk worker operation. Only this path permits the arranger to append a SONG MEMORY NOTES block after its normal Resonator track. Normal lane generation remains notation-only.
- Song memory notes capture section summary, important motifs, chord progressions, bass/rhythm cells, activated/AHD harmonic memory, carry-forward instructions, and an updated song-so-far summary.
- Normal composition stage status now reads Composing · Directing…, Composing · Arranging…, and Composing · Rendering MIDI….
- Standalone Resone remembers its normal window size/location and restores it only when at least 35% remains visible on a currently connected monitor. VST3-hosted windows are not repositioned.
- Full Export MIDI is now Standard MIDI Format 1: a conductor tempo/meter track plus one independent MIDI track per audible Resone lane.
- Added Drag Full Multitrack MIDI alongside per-lane MIDI drags; the full arrangement drag is generated locally and preserves separate lane tracks.

2026-09-17 Song mode workflow:
- Added persisted Song mode UI checkbox (first-run default off).
- Restored per-lane Song inclusion checkboxes using Lane.IncludeInAi. Unchecked lanes are not submitted in Song mode; normal single-lane Compose behavior is unchanged.
- Song mode calls the Producer once, then loops producer sections in order and checked lanes one at a time through the existing director -> composer path.
- Section generation is beat-scoped: section notes are sent locally from beat zero and returned notes are shifted back into the full-song timeline.
- Producer plain-text contract is intentionally compact: Overall identity, Motif strategy, Rhythmic strategy, Sections, Final payoff.
- Song-only composer memory contract is intentionally compact: Melody notes, Motifs, Important chords.
- Added answering-part composer guidance: relate answer notes to remembered/source notes using the narrative Continuation interval/perspective while varying rhythm and delivery.
- Song generation is one undo checkpoint and restores the pre-song state on cancel/error.


2026-09-17 direct local Resonator bridge:
- MIDI notation rendering, single-lane MIDI drag, full-project multitrack MIDI drag, and Export MIDI now call the Resonator/MidiFileWriter code inside wds.resone.api.dll synchronously in-process.
- Those operations do not traverse the launcher WebSocket. The WebSocket remains for AI generation and other launcher-owned services.
- C ABI version is now 2 and owns returned MIDI buffers until resone_free_buffer is called.

2026-09-17 window/song defaults:
- Standalone window bounds are now tracked on the real top-level HWND and saved on move/resize completion and close/destroy, then restored after the open stack via a posted message.
- Saved bounds restore only when at least 35% remains visible across connected monitors.
- New songs default only Melody to included in Song mode; Bass/Chords/Drums/Guitar/Strings and newly added lanes default unchecked. Explicit saved lane choices remain preserved.

2026-09-17 standalone window persistence follow-up:
- Fixed a resize feedback loop: OnParentWindowResize no longer calls the editor-to-host resize API.
- Normal standalone bounds now save the actual top-level GetWindowRect outer frame.
- Startup restore uses SetWindowPos after a short settle delay so iPlug's default 1440x850 startup sizing cannot overwrite the remembered dimensions.
- SIZE_RESTORED startup messages no longer rewrite window.json; user resize completion and close/destroy persist the final bounds.

2026-09-17 local inference configuration:
- `nativeInference:true` or `useLocalInference:true` selects the in-process GGUF adapter; either flag is accepted.
- `scripts/build-windows.ps1` now synchronizes the local-inference mode and native DLL/model settings into the installed appsettings instead of preserving a stale false value.
- Launcher startup prints `LLM transport: native (...)` or `LLM transport: HTTP ...` so the selected path is explicit.


2026-09-17 direct in-process llama.cpp inference:
- Local LLM generation no longer requires llama-server or port 8080 when local inference is enabled.
- Resone chooses a llama.cpp engine directory by runtime identifier; Windows x64 defaults to engines/llm/llama-cpp-dynamic-win-x64.
- resone_llama_bridge.dll is only a small ABI shim. It dynamically loads llama.dll, ggml.dll and the backend plugins from the selected downloaded engine pack.
- The GGUF is eagerly warmed before the AI stack reports ready and remains loaded until that worker/AI stack is stopped.
- Current local settings mirror the previous Resone stack behavior: 16384 context, 99 GPU layers with CPU fallback, flash attention on, temperature 0.7, top-p 0.95 and top-k 40.
- Thinking is disabled. Streaming output defensively strips a leading Gemma thought channel so reasoning cannot enter Resone notation, logs, or conversation history.
- llama.cpp runtime logging is suppressed; normal Resone request diagnostics remain available when enabled.

2026-09-17 build-folder runtime root fix:
- Running build/ui/out/Resone.exe now prefers the source/build tree it came from instead of silently falling back to an older %LOCALAPPDATA%/Wds/Resone installation.
- Development root discovery walks module/current-directory ancestors for config/appsettings.json plus build/api/wds.resone.api.dll.
- The build-tree UI loads build/api/wds.resone.api.dll and passes the actual source runtime root into the API; the API then launches build/launcher/wds.resone.launcher.exe with RESONE_HOME set to that same root.
- Config, engines and models therefore resolve relative to the active source tree during development. Installed builds still use the installed runtime root.
- User-writable preferences (window.json, song/settings state, licensing state) remain in %LOCALAPPDATA%/Wds/Resone/user.
- resone_inference.dll is obsolete and is not referenced by current runtime code. The installer deletes a stale copy from older installations. The only inference shim is resone_llama_bridge.dll, which dynamically loads the real platform llama.cpp engine (llama.dll/GGML backends).

2026-09-17 prebuilt llama engine packs (no llama source build):
- The Resone Windows build no longer clones, fetches, configures, or builds llama.cpp.
- Resone compiles only its tiny dynamic-loader bridge against a vendored ABI snapshot in native/inference/llama_dynamic_abi.hpp.
- The real llama.cpp runtime is supplied as a prebuilt ZIP by the launcher at runtime.
- Configure engine pack downloads in config/runtime.json (development) or a release runtime manifest. For Windows x64 local inference, use directory engines/llm/llama-cpp-dynamic-win-x64 and backend dynamic.
- The ZIP root should contain the runtime binaries directly, including llama.dll and ggml.dll plus whatever GGML CPU/CUDA backend DLLs that pack needs. Do not wrap them in another llama-cpp-dynamic-win-x64 directory inside the ZIP.
- Set useExistingStack=false to let the launcher provision packs/models. Provide an HTTPS URL and SHA256. The launcher downloads, hashes, extracts atomically, verifies requiredFiles, and records the pack hash so it is not downloaded again until the configured SHA changes.
- appsettings.json still selects the engine by RID through llamaEngineDirectories. win-x64 maps to engines/llm/llama-cpp-dynamic-win-x64.
- Local inference remains in-process in the launcher worker, with contextTokens=16384, reasoning disabled, streaming enabled, llama logs suppressed, and the model kept loaded while the AI stack is active.

2026-09-18 voice library + persistent song workspace:
- Vocals lanes now expose lyrics and a persistent saved-voice selector.
- New Voice can generate a Qwen VoiceDesign sample or import/drop a WAV, transcribes and validates it, previews/replays it, and only persists on OK.
- VoiceDesign is a separate lazy 1.7B Qwen context; it is released when the designer closes. The Base Qwen context handles saved reference-voice speech for vocal rendering.
- Saved voices retain their sample WAV + transcript and precomputed speaker/RVQ latents under user/voices, with the last selected voice persisted in library.json.
- Added 20 six-note Resonator singing-preview gestures for "We develop software".
- Song projects now persist as JSON-backed workspaces under user/songs with index/meta/project/history files, saved-song navigation, click-to-edit titles, delete confirmation, and Save & New.
- SongDesign now supplies a generated Song title that becomes the workspace title.
