RESONE: layout, publish content, and shared AI stack fix

Close Resone, extract this archive into U:\Resone and overwrite the included files.
If you edited the source config/appsettings.json or config/runtime.json, back them
up first and merge your custom values after extraction.
Run scripts\build-windows.ps1 again with your original arguments/InstallDir.
Run the installed launcher, not an older copy. Leave Six Stars running.

Changes:
- Corrects double DPI scaling of the WebView for the standalone app and VST3.
- Keeps Submit in its own space beside the shrinking textarea.
- Copies assets and config on dotnet build/publish, including music instructions.
- Stages assets explicitly and checks music-composition.json is installed.
- STT uses port 8000, matching Six Stars. LLM 8080 and TTS 8101 are unchanged.
- runtime.json useExistingStack defaults true: no duplicate model downloads or
  services; closing Resone does not stop Six Stars. Set false to let Resone manage
  its own configured runtimes when Six Stars is not running.
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
