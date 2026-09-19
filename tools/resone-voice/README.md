# Resone Voice quick renderer

This is intentionally **not** a copied vocal project. It references the same Resone API project and compiles the same `native/vocals/resone_vocals.cpp`, `native/vocals/resone_vocal_phonetics.cpp`, and `native/vocals/resone_vocal_articulation.cpp` files used by the application.

From the Resone root, either build command leaves a runnable executable in the normal build folder:

```powershell
.\build-voice.ps1
# or
.\build-voice-native.ps1
```

Both produce / preserve:

```text
build\voice\resone-voice.exe
build\voice\wds.resone.vocals.dll
```

Then run (the result plays automatically through DirectSound):

```powershell
.\build\voice\resone-voice.exe peppy amazing
```

`build-voice-native.ps1` is the fast DSP loop. If `resone-voice.exe` already exists it rebuilds only the native vocals DLL. On a fresh tree it automatically bootstraps the managed CLI once, so it never leaves you with only the DLL.

After changing any file under `native\vocals` (including the articulation/phonetics helpers):

```powershell
.\build-voice-native.ps1
.\build\voice\resone-voice.exe peppy amazing
```

Useful commands:

```powershell
.\build\voice\resone-voice.exe --list
.\build\voice\resone-voice.exe --voices
.\build\voice\resone-voice.exe peppy amazing --refresh-source
```

Runs go under `build\voice\out`; the reusable Qwen source phrase cache lives under `build\voice\cache`.

For silent/batch rendering:

```powershell
.\build\voice\resone-voice.exe peppy amazing --no-play
```
