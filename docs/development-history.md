# Development history
This file preserves incremental engineering handoff notes from earlier Resone development snapshots. It is historical reference only; current behavior is documented by the other files in `docs/`.

---

### Source: v06-piano-roll-composer-and-build-fixes.md

## Resone piano-roll + composer-context patch

Implemented:
- Added a left pitch-name ruler aligned to each lane's piano-roll rows.
- Added a prominent current Key / tonic badge to the piano-roll toolbar.
- Scale rows use lane accent tint; non-scale rows use a distinct gray tint; tonic rows are stronger and labeled TONIC in the ruler.
- Note blocks now scale continuously with vertical zoom instead of sitting at a 4px minimum for much of the zoom range.
- Increased vertical zoom range to 6x and added visible Y zoom controls in addition to Alt+wheel.
- Note names appear inside note blocks once their vertically zoomed height is large enough.
- Full-lane regeneration now replaces stale key metadata with the generated key.
- Manual piano-roll edits preserve the lane key when notation is reserialized.
- The Composer Design/Overview generated during Song mode is persisted with the saved song.
- All later normal lane modification requests receive the saved composer overview.
- Later full-song producer and composer-design requests also receive the previous composer overview, preserving the established musical DNA unless the user explicitly asks to change it.

Validation:
- JavaScript syntax check passed.
- Piano-roll, DOM, song-mode, song-resilience, generation-state, lane-notation, MIDI-import, workspace, composer-design, and composer-continuity source regression tests passed.
- A .NET solution build could not be run in this environment because the dotnet SDK is not installed.

Build follow-up fixes:
- Fixed `TrayIcon` CS0102 by renaming the private Win32 tray data struct from `Notify` to `NotifyIconData`; the public `Notify(...)` method and its callers remain unchanged.
- Applied the same unambiguous `NotifyIconData` naming to the updater Win32 wrapper.
- `build-windows.ps1` now fingerprints launcher + API managed build inputs. When the launcher is running and that fingerprint already matches a built launcher, its `dotnet publish` step is skipped.
- Because the launcher AOT-links `wds.resone.api`, API source changes correctly invalidate the launcher fingerprint.
- If a changed launcher must be rebuilt while the running process itself came from `build\\launcher`, the script publishes the new one to `build\\launcher-next` rather than attempting to overwrite the live executable.
- A new regression test checks the tray type/method collision and running-launcher skip contract.

## v4 updater compile correction
- Removed the duplicate top-level `TryLaunchLauncher` local function from `wds.resone.updater/Program.cs`.
- The update-failure fallback relaunch now calls the single canonical helper with an explicit `null` value for the flag-only `--update-failed` argument.
- Added updater regression coverage that requires exactly one `TryLaunchLauncher` helper and verifies the fallback call matches its full signature.

## v5 follow-up
- Piano-roll horizontal and vertical zoom are now stored per lane.
- Ctrl+wheel (or Cmd+wheel) changes only the selected lane's horizontal scale.
- Alt+wheel changes only the selected lane's vertical/pitch-row scale.
- X/Y toolbar controls follow the selected lane; Fit applies only to that lane.
- Note editing, beat seeking, note creation, key-region rendering, ruler rendering, and the playhead use the selected/owning lane's scale.
- The in-process llama bridge is now built to an immutable source-hashed DLL name during development. A running bridge no longer blocks rebuilds; unchanged bridge source reuses the existing hashed DLL, while changed source creates a new DLL. Installed releases still receive the canonical resone_llama_bridge.dll filename.

## v6 zoom scope correction
- Horizontal piano-roll zoom is global across the whole roll again. Ctrl/Cmd+wheel, the X slider, X +/- buttons, and Fit all change every lane's time scale together.
- Vertical piano-roll zoom remains lane-local. Alt+wheel and the Y controls resize only the selected lane's pitch rows and notes.
- Removed persisted per-lane horizontal zoom state and clear the old `resone.pianoRoll.laneZoomX` preference during preference saves.
- Updated regression coverage to enforce global X / lane-local Y zoom behavior.

---

### Source: v07-production-licensing-and-installer.md

## Production licensing + installer patch

This patch is based on the v6 piano-roll/live-build source and adds the production distribution path.

## Customer licensing

- Uses the existing Cloudflare Worker + D1 licensing project.
- A purchased `RSN-...` license checks out to one non-exportable Windows CNG device key.
- Signed offline leases remain device-bound.
- License activation state is stored under Windows DPAPI.
- A new `/v1/instruction-key` proof action returns the protected instruction AES key only to the activated device while its lease is live.
- The locally cached instruction key is stored inside the same DPAPI-protected activation cache.

## Protected LLM instructions

- Customer builds no longer embed the AES key in `PackedInstructions.g.cs`.
- `pack-instructions.mjs` requires a private 32-byte base64 instruction key from a file/environment variable.
- Generated code contains only AES-GCM nonce, tag, and ciphertext.
- The API cannot read protected instructions until `LicenseService` installs a server-delivered key.
- Customer builds require `-InstructionKeyFile`; loose `assets/Instructions` remain prohibited from release staging.

## Installer

- Added an Inno Setup installer project under `installer/Resone.iss`.
- First install presents a masked license-key field.
- Existing activated installs skip the key page on upgrade.
- The key is handed to the launcher via a temporary file, not on the process command line, then the temp file is deleted.
- Setup attempts activation after file installation and does not launch Resone if activation fails.
- Installs the Resone application plus its VST3 bundle for the current user.

## Production build scripts

- `scripts/build-customer-release.ps1` builds a clean customer staging tree then invokes Inno Setup.
- `scripts/build-installer.ps1` discovers Inno Setup 7 or 6, or accepts an explicit compiler path.
- `docs/production-build-and-release.md` documents Worker secrets, production packaging, and activation flow.

## Cloudflare secret setup

Fresh deployments create `INSTRUCTION_KEY_BASE64` with the other secrets. Existing deployments can run `cloud/licensing/scripts/create-instruction-key.mjs`, upload the result with Wrangler, and use the same private file during customer packaging.

## Validation performed in this environment

- Cloudflare Worker unit tests: 6/6 pass, including activated-device-only instruction key delivery.
- Worker and instruction packer JavaScript syntax checks pass.
- Customer instruction bundle AES-GCM encrypt/decrypt smoke test passes.
- Generated packed instruction C# verified to contain no AES key constant.
- API/launcher project XML parsed successfully.
- Static checks confirm no `.secrets` or instruction-key file is included in the project package.

The Windows .NET/AOT build, CMake build, and Inno compiler cannot be executed in this Linux sandbox and should be run on the normal Windows build machine.

---

### Source: v09-vocals-playback-and-installer.md

## Resone v9 patch notes

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

---

### Source: v10-genre-brief-retrieval.md

## Resone v10 — Genre Brief Retrieval

## Added
- Added `resone_song_design_genre_guide.md` and `resone_drums_genre_guide.md` to `assets/Instructions/Music`.
- Added deterministic `GenreBriefResolver` with canonical-ID/alias parsing, typo-tolerant fuzzy matching, parent genre support, explicit hybrid handling, optional modifiers, and fail-soft behavior.
- Producer receives a best-effort genre brief from the user's original request before it writes the song identity.
- After producer output, Resone extracts `Overall identity` and re-resolves the final canonical genre from that identity; the producer identity outranks the original request.
- Composer Design receives the final song-design and drum briefs.
- Song generation state persists the selected genre IDs and compact genre context.
- Every later song Director and Composer call receives the same selected genre context through `FULL SONG GENERATION CONTEXT`.
- `DesignSongAsync` returns `genreId` for diagnostics/UI use.

## Matching behavior
- Specific aliases outrank generic parent genres.
- Typo-tolerant token matching supports near matches such as `progressive metel`.
- Explicit `trap metal` is treated as the documented trap + metal hybrid.
- Cinematic/orchestral language can add `cinematic_orchestral` as a secondary role rather than replacing a more specific primary genre.
- Unknown/genre-neutral requests simply receive no genre injection; generation continues normally.

## Production protection
- Both genre guides were added to the encrypted customer instruction bundle.
- The instruction bundle test now validates seven protected files and confirms the AES key is not embedded in generated C#.

## Tests
- Added `tests/genre-guide-retrieval-regression.py`.
- Existing composer continuity, song mode, workspace, piano-roll, updater, live Llama bridge, and encrypted instruction-bundle regressions pass in this environment.
- The .NET SDK is not installed in this sandbox, so the Windows/.NET compiler build still needs to be run locally.

## Future drum-guide expansion
The retrieval code reads canonical IDs and aliases from the Markdown at runtime. You can expand the current drum entries without changing code as long as canonical IDs remain compatible with the song-design guide. New aliases are picked up automatically.

---

### Source: v11-drum-percussion-grammar.md

## Resone v11 — Merged Drum/Percussion Grammar

## What changed

- Merged `resone_drum_percussion_grammar_v3` into the protected `resone_drums_genre_guide.md`.
- Added a compact `drum_core` that is supplied on every non-empty song-generation genre lookup, even when no genre match is confident.
- `drum_core` includes the Resone K/S/H/O/L/M/T/C/R note map, universal rhythmic atoms, energy controls, section dynamics, phrase defaults, fill behavior, and the minimal drum decision procedure.
- Drum genre matching is now independent from song-design genre matching. The Producer can choose a broad song identity while percussion retrieves a more specific grammar such as deep house, classic trance, Goa, jungle, grunge, nu metal, etc.
- Drum retrieval walks the complete family chain broad-to-specific. Examples:
  - `edm -> trance -> uplifting_trance`
  - `edm -> house -> deep_house`
  - `edm -> drum_and_bass -> jungle`
  - `classical -> romantic_classical -> late_romantic`
  - `rock -> grunge`
  - `metal -> nu_metal`
- Song-design retrieval also now walks all available ancestors instead of stopping after only one parent.
- Explicit hybrid roles still remain separate instead of averaging incompatible grooves. `trap metal`, for example, retains its hip-hop/trap function and metal function as separate inherited jobs.
- The merged guide retains the complete uploaded Drum & Percussion Grammar in a source appendix for maintenance/reference while runtime retrieval injects only the relevant compact blocks.
- Increased stored song genre-context allowance from 16k to 26k characters so universal + family + specific genre information is not prematurely clipped.
- Song generation state now records the independently selected drum genre and resolved drum-family chain.
- The merged guide remains part of the encrypted production instruction bundle; the production AES key is still not embedded in generated C#.

## Validation

Passed in this environment:

- genre-guide retrieval regression
- encrypted production instruction-bundle round trip
- song composer design pass
- composer-overview continuity
- song-composer continuity
- generation-state/song-mode regressions
- piano-roll interaction regression
- updater regression
- live Llama bridge build regression

The Windows .NET compiler is not installed in this sandbox, so the local Windows build remains the definitive C# compiler check.

---

### Source: v12-compact-drum-genre-retrieval.md

## Resone v12 — Compact Genre Context + Melody-Mode Drum Genre Identification

## Drum genre context is compact and lane-scoped

- The always-on `drum_core` runtime brief is now a compact fundamentals block (kit map, pulse, kick/snare/hat policy, fills, energy, phrase behavior, central genre rule).
- The full merged Drum & Percussion Grammar remains in the protected guide as a source appendix, but is not injected wholesale into LLM requests.
- Drum retrieval still inherits broad-to-specific fundamentals, e.g. `drum_core -> edm -> trance -> uplifting_trance`.
- Song-mode pitched/vocal lanes receive song-design genre guidance only; drum grammar is added only to drum-lane packets.
- Song mode still gives the Producer and global Composer Design pass the combined selected genre context so they can plan the whole arrangement.

## Melody-mode drum genre identification

For ordinary non-Song-mode lane generation only:

1. If the selected lane is a drum lane, Resone makes one tiny `DrumGenreIdentification` LLM request.
2. The model returns one short genre/subgenre string (or `NONE`).
3. Resone fuzzy-matches that label against the local deterministic genre guide.
4. The resulting compact drum brief is supplied to both the Director and Composer.
5. If the classifier returns `NONE`, an unfamiliar label, or errors, Resone falls back to deterministic matching against the user request and ultimately the compact drum core. Generation never fails because genre identification failed.
6. Revision requests include the lane's original brief and recent update prompts in the tiny classifier request, so `more energy` can retain a prior `trance drums` identity.

No extra genre-identification LLM call runs for:

- Song mode (the Producer's `Overall identity` already owns genre selection),
- pitched melody/bass/chord/accent lanes,
- vocal lanes.

## No artificial LLM context/output clipping

- Removed the v11 `MaxGenreContextChars` limit rather than increasing it.
- Removed genre-context slicing from Producer and Composer Design prompts.
- Removed the 65,536-character streaming output guard from both HTTP and in-process llama.cpp clients; EOS/model context is the generation limit.
- Removed arbitrary character clipping from Song-mode producer design, composer design, section memory, exact musical memories, and open composer commitments.
- Removed producer/composer design persistence truncation in workspaces.
- Existing semantic validation such as valid notation, meter, lane count, etc. remains intact.

## Compatibility

- `GenreContext` remains on `SongGenerationState` only for loading older saved state.
- New states store `GenreSongContext` and `GenreDrumContext` independently.

## Validation

Passed in this environment:

- genre guide retrieval regression
- song section parser regression
- piano-roll interaction regression
- updater regression
- launcher live-build regression
- llama bridge live-build regression
- UI DOM contract regression
- unlimited LLM generation regression
- Song mode regression
- Composer Design pass regression
- composer continuity / handoff commitments
- composer overview workspace continuity
- song workspace regression
- generation-state regression
- encrypted seven-file instruction bundle round-trip

The Windows .NET SDK is not installed in this sandbox, so the actual Windows compiler/build still needs to be run locally.

---

### Source: legacy-development-fix-notes.md

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

---

### Source: legacy-patch-install-readme.md

Apply over the current patched project. Back up custom music-composition.json.
Extract into U:\Resone, rebuild and restart launcher and editor.

LLM output is now plain text received over the existing SSE stream:
track=<exact-lane-id> tempo=120 4/4
C4 E4 G4 C5
track=<another-lane-id> tempo=120 4/4
C3 _ G3 _

Each track header starts on its own line; notes may follow on that same line.
A subsequent header or end of response terminates the track. Body syntax still
uses resonator_api_v0.1.md. Both generation and repair prompts live in JSON.

Missing lanes are accepted. Valid tracks survive malformed tracks, unknown IDs,
and duplicates. First valid occurrence of each requested ID wins. If at least
one track is playable, no repair is requested. Omitted lanes remain empty rather
than replaying old music. All-invalid responses get the existing one repair.
Internal application messages remain JSON; LLM-generated notation does not.

This changes output framing and partial-result acceptance, not playback timing:
LLM text streams, but playback still waits for end-of-response parsing.
Verified API build zero warnings/errors, text-header decoding, missing-track
acceptance, drum mapping, invalid-track isolation and UI partial autoplay.
Live LLM and Windows playback not verified.

---

## v15 piece notes viewer

- Added a document icon beside the current song title in the top composer panel.
- The icon toggles a read-only Piece notes drawer without changing the active editing lane.
- The drawer shows the saved Producer design and Composer design/overview for the selected song in separate scrollable panes.
- Empty states explain when Producer or Composer notes have not been generated yet.
- Loading another saved song and completing a new Song-mode design refresh the drawer from that song's persisted workspace metadata.
- Notes are rendered with `textContent`, remain selectable for reference/copying, and cannot accidentally mutate generation context.


## 2026-09-19 — compact CPU/CUDA/Vulkan runtime manifest

- Added first-class support for the compact `enginePacks` backend matrix: one entry with `cpu`, `cuda`, and `vulkan`, each containing `llm`, `tts`, and `asr` downloads.
- Compact entries derive IDs, version, RID, install directory, and default required files while preserving the old flat engine-pack format for backward compatibility.
- Hardware mapping is now NVIDIA -> CUDA, AMD/Intel -> Vulkan, otherwise CPU; old `nvidia`/`amd`/`intel` override names still normalize correctly.
- Updated customer-release validation to understand compact engine entries and continue requiring HTTPS plus real SHA-256 hashes.
- Updated `runtime.release.json` / example with the current model hashes and official llama.cpp/whisper.cpp archive hashes. CPU/Vulkan Whisper use the CPU archive; CUDA Whisper uses the cuBLAS 12.4 archive, and all three ASR entries strip the archive's top-level `Release/` directory during installation. Qwen TTS package URLs/hashes remain placeholders until those custom archives are published.

### Worker domain-verification file

- Added `GET /01a0bc28-2ec7-7db3-abe1-a20b8f279de8.txt` to the Cloudflare licensing Worker.
- The route returns the exact plain-text verification token `B8FxYSQms8N98euAXGKJ8YUuY8Y` and does not require D1 or licensing secrets.
- Added an automated Worker test for the response status, content type, and exact body.


## v19 — release-ready LLM backends

- Production runtime manifest now enables the Gemma model plus llama.cpp CPU, CUDA/NVIDIA, and Vulkan backends while leaving unfinished TTS/ASR engines and models disabled.
- Added verified companion-archive support for engine packs; the CUDA llama.cpp package now brings its matching CUDA 13.3 runtime DLL archive.
- Added `scripts/build-release.ps1` as the normal one-command Windows customer release entrypoint using the local Cloudflare public signing key, instruction key, production runtime manifest, and `https://licenses.resone.io`.
- Customer-build validation checks hashes/HTTPS for enabled companion archives as well as primary engine archives.

## Runtime self-repair and deterministic engine directories

- Release CPU/CUDA/Vulkan LLM packs now install into the configured `engines/llm/llama-cpp-dynamic-win-x64` directory used by `appsettings.json`; backend selection remains hardware-driven and only one selected LLM pack occupies that directory at a time.
- The launcher always checks that the selected engine directory/required runtime files and enabled model file actually exist. Missing files trigger provisioning immediately.
- Engine archives are SHA-256 verified on download. After extraction, SHA-256 values for the installed engine files are cached in the engine receipt.
- Model downloads are SHA-256 verified once, then normal startup uses the verified receipt plus file length/timestamp as the fast path instead of re-hashing the multi-GB GGUF on every launch.
- Native/local LLM startup or generation failures create `AI_ROOT/state/llm-reverify.required`. The launcher consumes that marker on the next ensure/restart, performs a full engine-file + model SHA verification, re-downloads anything invalid/missing, and automatically retries worker startup once.

## v21 — optional release protection / trust-based production build

- Added `release.licensingEnabled` and `release.encryptInstructions` to the release manifest; both are currently `false`.
- Production builds no longer inherently require Cloudflare licensing or encrypted LLM instructions.
- With licensing disabled, the release build does not require the public signing JWK or a licensing endpoint, emits `config/licensing.json` with licensing disabled, and the launcher does not gate AI operations on activation.
- With instruction encryption disabled, production builds stage the ordinary `assets/Instructions/Music` files and `InstructionContent` reads them directly.
- Encrypted instructions remain available as an opt-in mode; enabling them still requires licensing because the current key-delivery flow is device-licensed.
- The Inno installer receives the licensing flag from the release manifest. With licensing disabled, it omits the license-key/activation page and launches Resone normally after installation.
- Studio Settings hides the license controls when the connected launcher reports licensing disabled.
- Older release manifests without the new `release` object preserve the previous licensed/encrypted behavior for backward compatibility.
