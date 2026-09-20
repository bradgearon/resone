# Shared AI runtime provisioning

Resone's launcher owns provisioning for large AI engines and models. The runtime is intentionally outside the Resone installation so Resone, Six Stars, and future WDS applications can share one verified copy.

## AI_ROOT

Resolution order:

1. `wds.resone.launcher.exe --ai-root <path>` (also persists the choice)
2. `AI_ROOT` environment variable
3. legacy `WDS_AI_ROOT` environment variable
4. saved `%LOCALAPPDATA%\Wds\AI\root.txt`
5. default `%LOCALAPPDATA%\Wds\AI`

The root contains:

```text
AI_ROOT/
  engines/
    llm/
    asr/
    tts/
  models/
    llm/
    asr/
    tts/
  work/
```

Example:

```powershell
.\wds.resone.launcher.exe --ai-root D:\WdsAI
```

or set a shared environment variable before starting either product:

```powershell
$env:AI_ROOT='D:\WdsAI'
```

Six Stars should use the same `AI_ROOT` resolution contract when it is migrated.

## Manifest

`config/runtime.json` is the launcher provisioning manifest. Large package metadata is data, not launcher code. Each engine/model has a stable `id`, a publisher-controlled `version`, HTTPS `url`, and SHA-256.

Changing any managed package `version`, `url`, or `sha256` makes the launcher replace that package on the next start. Downloads are staged and verified before an engine directory is atomically swapped into place. Models use resumable `.partial` files and are moved into place only after verification.

Engine packs also declare:

- `component`: `llm`, `asr`, or `tts`
- `rid`: for example `win-x64`
- `platform`: `nvidia`, `amd`, `intel`, `apple`, `cpu`, or `dynamic`
- `directory`: destination beneath `AI_ROOT`
- `requiredFiles`: sanity check after extraction
- optional `stripComponents`: number of leading ZIP path components to remove

The launcher detects the display-adapter vendor on Windows and chooses an exact platform pack when available, then `dynamic`, then `cpu`. `AI_PLATFORM=nvidia|amd|intel|apple|cpu` overrides detection for testing.

After selection, the launcher writes `engines/<component>/.active`. Runtime code follows that pointer, so appsettings does not need a different path for every GPU vendor. If there is no managed active engine yet, development builds fall back to the existing configured source-tree engine path.

## Publishing a new engine version

1. Build/stage the engine directory.
2. Create an archive and SHA-256:

```powershell
.\scripts\package-ai-engine.ps1 `
  -SourceDirectory .\engines\tts\qwenttscpp-nvidia-win-x64 `
  -OutputZip .\artifacts\qwenttscpp-nvidia-win-x64-v2.zip
```

3. Upload the ZIP to HTTPS storage.
4. Update its manifest entry: `version`, `url`, `sha256`; set `enabled: true` when ready.
5. On the next launcher start, machines for that RID/platform install the new pack before the worker loads it.

## Publishing a model version

Hash a model with:

```powershell
.\scripts\hash-ai-model.ps1 -Path .\models\tts\model.gguf
```

Update `id` (normally stable), `version`, `path`, `url`, and `sha256` in the manifest. A managed model with a changed version/URL/hash is intentionally redownloaded even if the destination filename is unchanged.

## Native source pins

`dependencyPins` in `config/runtime.json` is the source of truth for C++ dependency repositories/git refs. Current build scripts consume it for qwentts.cpp, llama.cpp ABI validation, iPlug2, VST3 SDK, and vcpkg. Do not put a second commit constant into a build script.

The launcher does not trust a downloaded engine archive merely because its filename/version matches. Enabled downloads require HTTPS and a real SHA-256; the ZIP is hashed before extraction, extracted paths are constrained beneath the staging directory, symlinks are rejected, required runtime files are checked, and only then is the engine activated.
