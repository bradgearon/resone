# Resone licensing and background-host update

This is a developer source update, not a prebuilt customer installer. The source baseline was recovered from the Resone starter project and the saved patches through `resone-saved-lane-notation.zip`; it is not a copy of your live U:\Resone checkout. Compare the focused diff before replacing files if you have made additional code changes there.

All five instruction files you supplied on this turn are copied byte-for-byte to `assets/Instructions/Music`. No musical wording has been rewritten. Your newly uploaded logo is used for the web logo and Windows application/tray icon resources.

## Included

- `cloud/licensing`: Cloudflare Worker, D1 migration, signed device challenges and leases, verified-purchase issuance endpoint/script, activation, renewal, release, revocation, and device reset. See its README for deployment and storefront integration.
- One launcher per user, shared by VST instances and the standalone app. Opening the editor asynchronously starts it; DAW scans do not start the launcher. Named pipes are used only for authenticated per-user bootstrap/readiness. Requests/results use WebSockets; no health polling.
- Launcher tray: Open Resone, Toggle AI stack, Exit Resone services. Closing the standalone editor leaves the launcher alive.
- Composition host runs in a separate child process. The launcher restarts it after an unexpected exit, with a three-restart limit. Clients reconnect after disconnects and discard interrupted requests rather than replaying compositions or activations. `--no-ui` and `--no-services` are retained.
- Downloadable engine packs selected by OS/architecture/backend, with CPU fallback per engine directory. HTTPS plus mandatory pack SHA256, archive traversal/symlink checks, extraction limits, staging and replacement rollback. Archive work directories are deleted after installation. Model downloads resume interrupted transfers, verify configured hashes, and replace corrupted models.
- Native llama.cpp adapter and C# client: model stays loaded, requests are serialized on a background thread, model chat template/Jinja is applied with thinking disabled, output is delivered through callbacks, cancellation is supported during evaluation. Native crashes stay outside the DAW. CPU operation is supported; GPU use requires a compatible GPU pack. Native mode no longer sends private prompts over HTTP.
- Release-only encrypted embedded instruction bundle, pinned public license-verification key, and prompt-file logging disabled at compile time. Developer builds retain editable Markdown/JSON and configurable logging. Embedded encryption deters casual extraction; it cannot make local prompts secret from a determined machine owner.
- Licensing controls in Settings. CNG device key and DPAPI license cache for Windows. Existing music remains editable/playable/exportable without contacting the license server; generation/transcription/TTS require activation in customer builds.
- Native MIDI drag button on each lane output. It stages a real `.mid` from current edited notes locally and uses Windows OLE file drag. File contents include tempo, meter, instrument program, exact note velocities/timing and percussion channel 10. Drag files remain in `%LOCALAPPDATA%\Wds\Resone\user\midi-drags` so deferred DAW imports can read them; delete them when no longer needed.

The existing greedy PCM playback renderer is retained. The recovered selected-lane composer validates a complete generated lane before committing/autoplaying it; this update does not add incremental notation-to-piano-roll playback during token generation. The native inference callbacks provide streamed text, but that is distinct from streaming playable MIDI. Test musical/model behavior before replacing your working engine configuration.

## Build a developer version

Run `scripts/build-windows.ps1` on Windows with .NET 9 NativeAOT prerequisites, Visual Studio C++ tools, CMake, Git and Node available. This retains HTTP inference against the existing Six Stars stack by default (`nativeInference:false`, `useExistingStack:true`). Launcher auto-start and MIDI drag still apply. All native and managed components must be rebuilt together because the bridge now exports `resone_set_home`.

To try native inference, build the adapter using `scripts/build-inference-pack.ps1`, extract the pack to `engines/llm`, and set `nativeInference:true` in the installed appsettings. Configure `nativeModelPath`, `nativeLibraryPath`, `contextTokens`, `gpuLayers` and `allowCpuFallback`. A CPU-only test uses `gpuLayers:0`. Restart the stack after changing model or native library. The pinned llama.cpp revision is in `native/inference/llama-commit.txt`; do not mix a different revision's common headers/libraries with this adapter.

The CPU pack script disables AVX/AVX2/FMA/F16C for a broad baseline. Optimized CPU builds and GPU pack dependencies should be tested on their actual target machines. The native core is portable, but the current iPlug2 WebView editor, tray and protected device-key integration are Windows implementations. macOS/Linux desktop releases still need their platform UI/tray/keystore/drag implementations; their names in the manifest are not a claim that those applications are complete.

## Prepare a customer release

1. Deploy the licensing worker using `cloud/licensing/README.md`. Test purchase issuance and activation before compiling your customer build.
2. Copy `config/runtime.release.example.json` to your private production manifest. Fill real engine/model download URLs and SHA256 values. Supply at least Windows x64 CPU LLM and ASR packs. The LLM archive root must contain `resone_inference.dll`; the ASR archive root must contain the configured executable (example `whisper-server.exe`). TTS is optional and needs its own pack, models and service entry when enabled. The example intentionally contains no invented URLs/hashes.
3. If using CUDA, add a CUDA LLM pack for the same `engines/llm` directory. With backend `auto`, the launcher checks for the driver library and prefers that pack; missing GPU-specific packs for other engines fall back to CPU. Driver-library presence is a heuristic; validate actual runtime compatibility. A model/context allocation failure can fall back to CPU within the loaded native runtime. A DLL that cannot load due to missing GPU dependencies still requires a corrected pack or choosing CPU.
4. Run, with your actual values and a fresh staging directory:

```powershell
./scripts/build-windows.ps1 `
  -CustomerRelease `
  -InstallDir 'U:\Resone\dist\Resone-customer' `
  -LicensePublicKeyFile 'U:\private\signing-public.jwk' `
  -LicenseApiUrl 'https://YOUR-DEPLOYED-WORKER' `
  -RuntimeManifest 'U:\private\runtime.production.json'
```

The build requires the public key and valid download configuration, publishes NativeAOT components, omits loose instruction files, disables prompt logs, and stages the icon/UI/VST. It refuses a nonempty customer staging directory to avoid shipping old logs or instructions. Ship the contents of the customer staging directory, not this source archive or `cloud/licensing/.secrets`.

The canonical installed location for discovery from a copied VST is `%LOCALAPPDATA%\Wds\Resone`; alternatively set `RESONE_HOME` to the installation folder. The customer app itself can open from its install directory, and an installer can set that path/distribute a launcher shortcut. This package does not include a signed MSI, automatic DAW VST folder installation, a storefront webhook, or deployed cloud resources.

## Verification and remaining release tests

Completed here: Worker behavior tests, actual local Workers/D1 concurrent-activation test, Wrangler deployment dry run, editor generation/notation regression tests, encrypted-bundle round trip, C# compilation including the customer configuration, Linux native inference compilation/ABI/error smoke test, and portable MIDI export checks. See `VALIDATION.md` for the final NativeAOT outcome.

Before shipping: run the Windows NativeAOT/iPlug2 build, open the standalone and multiple DAW instances, confirm one tray host, test microphone and drag/drop in your DAWs, activate/renew/release with the real Worker, test GPU/CPU packs on clean machines, and generate with your actual GGUF. The required model and Windows DAW environment were not available for those tests here. No customer keys were generated or deployed.

The local worker accepts only the bootstrap session token and rejects browser Origin headers. A custom external API URL can still target a compatible separately managed host; the managed Resone worker itself is loopback-only. Existing independently running Six Stars services are never stopped when `useExistingStack` is enabled.
