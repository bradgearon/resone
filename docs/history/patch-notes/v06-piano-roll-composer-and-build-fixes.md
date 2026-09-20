# Resone piano-roll + composer-context patch

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
