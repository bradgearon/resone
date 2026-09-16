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
