# Resone production build

Production packaging and copy protection are separate concerns. `config/runtime.release.json` controls whether a release uses licensing and whether its LLM instruction files are encrypted.

The current release configuration intentionally ships as a trust-based build:

```json
"release": {
  "licensingEnabled": false,
  "encryptInstructions": false
}
```

With those values, a production build does **not** require the Cloudflare licensing Worker, signing keys, or an instruction AES key. The installer does not show a license-key page, the application does not gate AI operations on activation, and the normal instruction files are staged under `assets/Instructions/Music`.

## Build the Windows customer installer

Install Inno Setup 6 or 7, then run:

```powershell
.\scripts\build-release.ps1 -Version 1.0.0
```

The wrapper reads `config/runtime.release.json`, stages the customer tree under `build/customer-staging`, and produces the Inno Setup EXE under `build/installer`.

The build always validates the enabled AI runtime entries. The release manifest uses the compact CPU/CUDA/Vulkan engine matrix; every enabled engine/model URL and companion archive must use HTTPS and every enabled download must have a real 64-character SHA-256 hash.

The checked-in release manifest is currently LLM-only: llama.cpp CPU, CUDA/NVIDIA, and Vulkan are enabled along with the Gemma model. TTS and ASR engine entries remain in the manifest but are `enabled: false`, and their models are also disabled, so unfinished Qwen/Whisper packaging does not block the installer build. The CUDA LLM entry installs llama.cpp's matching CUDA runtime DLL archive through `additionalArchives`.

## Release protection switches

`release.licensingEnabled` controls device licensing:

- `false`: no license secret/public key is required, `config/licensing.json` is emitted with `enabled: false`, the installer skips activation, and the Studio Settings license controls are hidden once the local host connects.
- `true`: the release embeds the public signing JWK, requires an HTTPS licensing endpoint, and restores the normal device activation/lease flow.

`release.encryptInstructions` controls production instruction packaging:

- `false`: the normal instruction files are copied into the customer build as plain files under `assets/Instructions/Music`.
- `true`: the build requires `INSTRUCTION_KEY_BASE64`, encrypts the instruction bundle, excludes the loose instruction directory, and reads the bundle through `InstructionContent` at runtime.

Encrypted instructions currently depend on the licensing service to deliver the AES key, so `encryptInstructions: true` requires `licensingEnabled: true`. Older release manifests with no `release` object retain the previous behavior and are treated as if both switches were `true`.

## Optional Cloudflare licensing setup

You only need this section if you turn `licensingEnabled` back on.

From `cloud/licensing`:

1. `npm ci`
2. Generate the licensing secrets with the existing scripts.
3. Upload the Worker secrets with Wrangler.
4. Create/apply the D1 database and deploy the Worker as described in `cloud/licensing/README.md`.
5. Keep the private keys and `INSTRUCTION_KEY_BASE64` out of source control.

When licensing is enabled, `scripts/build-release.ps1` expects `cloud/licensing/.secrets/signing-public.jwk`. When instruction encryption is also enabled, it additionally expects `cloud/licensing/.secrets/INSTRUCTION_KEY_BASE64`.
